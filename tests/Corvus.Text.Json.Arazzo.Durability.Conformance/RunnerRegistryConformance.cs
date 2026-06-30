// <copyright file="RunnerRegistryConformance.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Globalization;
using System.Text.Json;
using Corvus.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.Conformance;

/// <summary>
/// The shared contract every <see cref="IRunnerRegistry"/> must satisfy, regardless of backend: register
/// (and replace), heartbeat, list, and prune. A backend's test project derives a concrete
/// <see cref="TestClassAttribute"/> from this and implements <see cref="CreateRegistryAsync"/>; the in-memory
/// registry is the reference implementation and runs the same suite.
/// </summary>
public abstract class RunnerRegistryConformance
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly List<IAsyncDisposable> disposables = [];

    /// <summary>Creates a fresh, empty registry backed by the implementation under test.</summary>
    /// <returns>The registry.</returns>
    protected abstract ValueTask<IRunnerRegistry> CreateRegistryAsync();

    /// <summary>Disposes any registries created during the test.</summary>
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
    public async Task List_of_empty_registry_is_empty()
    {
        IRunnerRegistry registry = await this.NewRegistryAsync();
        (await registry.ListAsync(default)).ShouldBeEmpty();
    }

    [TestMethod]
    public async Task Register_then_List_returns_the_runner_with_its_fields()
    {
        IRunnerRegistry registry = await this.NewRegistryAsync();
        await registry.RegisterAsync(
            Reg("runner-1", T0, T0, maxConcurrency: 8, transports: ["http", "amqp"], hosted: [("shipping", 3, "abc123", true)], address: "https://runner-1.local"),
            default);

        IReadOnlyList<RunnerRegistration> runners = await registry.ListAsync(default);
        runners.Count.ShouldBe(1);

        RunnerRegistration runner = runners[0];
        ((string)runner.RunnerId).ShouldBe("runner-1");
        ((long)runner.MaxConcurrency).ShouldBe(8);
        ((string)runner.Address).ShouldBe("https://runner-1.local");
        runner.Transports.GetArrayLength().ShouldBe(2);
        runner.HostedVersions.GetArrayLength().ShouldBe(1);

        RunnerRegistration.RunnerHostedVersion hosted = runner.HostedVersions[0];
        ((string)hosted.BaseWorkflowId).ShouldBe("shipping");
        ((long)hosted.VersionNumber).ShouldBe(3);
        ((string)hosted.Hash).ShouldBe("abc123");
        ((bool)hosted.Loaded).ShouldBeTrue();
    }

    [TestMethod]
    public async Task Register_with_same_id_replaces_the_runner()
    {
        IRunnerRegistry registry = await this.NewRegistryAsync();
        await registry.RegisterAsync(Reg("runner-1", T0, T0, maxConcurrency: 4), default);
        await registry.RegisterAsync(Reg("runner-1", T0, T0, maxConcurrency: 16), default);

        IReadOnlyList<RunnerRegistration> runners = await registry.ListAsync(default);
        runners.Count.ShouldBe(1);
        ((long)runners[0].MaxConcurrency).ShouldBe(16);
    }

    [TestMethod]
    public async Task Register_keeps_distinct_runners()
    {
        IRunnerRegistry registry = await this.NewRegistryAsync();
        await registry.RegisterAsync(Reg("runner-1", T0, T0), default);
        await registry.RegisterAsync(Reg("runner-2", T0, T0), default);

        IReadOnlyList<RunnerRegistration> runners = await registry.ListAsync(default);
        runners.Select(r => (string)r.RunnerId).OrderBy(s => s).ShouldBe(["runner-1", "runner-2"]);
    }

    [TestMethod]
    public async Task Heartbeat_of_known_runner_advances_last_seen_and_returns_true()
    {
        IRunnerRegistry registry = await this.NewRegistryAsync();
        await registry.RegisterAsync(Reg("runner-1", T0, T0), default);

        DateTimeOffset later = T0.AddMinutes(5);
        bool updated = await registry.HeartbeatAsync("runner-1", later, default);
        updated.ShouldBeTrue();

        RunnerRegistration runner = (await registry.ListAsync(default)).Single();
        LastSeen(runner).ShouldBe(later);
        ReadStarted(runner).ShouldBe(T0);
    }

    [TestMethod]
    public async Task Heartbeat_of_unknown_runner_returns_false()
    {
        IRunnerRegistry registry = await this.NewRegistryAsync();
        (await registry.HeartbeatAsync("ghost", T0, default)).ShouldBeFalse();
    }

    [TestMethod]
    public async Task Prune_removes_runners_last_seen_before_the_cutoff_and_keeps_the_rest()
    {
        IRunnerRegistry registry = await this.NewRegistryAsync();
        await registry.RegisterAsync(Reg("stale", T0, T0), default);
        await registry.RegisterAsync(Reg("fresh", T0, T0.AddMinutes(10)), default);

        int removed = await registry.PruneAsync(T0.AddMinutes(5), default);
        removed.ShouldBe(1);

        IReadOnlyList<RunnerRegistration> runners = await registry.ListAsync(default);
        runners.Select(r => (string)r.RunnerId).ShouldBe(["fresh"]);
    }

    [TestMethod]
    public async Task Prune_with_no_stale_runners_returns_zero()
    {
        IRunnerRegistry registry = await this.NewRegistryAsync();
        await registry.RegisterAsync(Reg("runner-1", T0, T0.AddMinutes(10)), default);

        (await registry.PruneAsync(T0.AddMinutes(5), default)).ShouldBe(0);
        (await registry.ListAsync(default)).Count.ShouldBe(1);
    }

    [TestMethod]
    public async Task IsVersionHosted_is_true_only_for_a_loaded_hosted_version()
    {
        IRunnerRegistry registry = await this.NewRegistryAsync();
        await registry.RegisterAsync(
            Reg("runner-1", T0, T0, hosted: [("shipping", 3, "h", true), ("billing", 1, "h", false)]),
            default);

        (await registry.IsVersionHostedAsync("shipping", 3, default)).ShouldBeTrue();
        (await registry.IsVersionHostedAsync("billing", 1, default)).ShouldBeFalse();  // hosted but not loaded
        (await registry.IsVersionHostedAsync("shipping", 4, default)).ShouldBeFalse();  // wrong version
        (await registry.IsVersionHostedAsync("other", 3, default)).ShouldBeFalse();     // wrong base
    }

    [TestMethod]
    public async Task IsVersionHosted_is_false_after_the_hosting_runner_is_pruned()
    {
        IRunnerRegistry registry = await this.NewRegistryAsync();
        await registry.RegisterAsync(Reg("runner-1", T0, T0, hosted: [("shipping", 3, "h", true)]), default);
        (await registry.IsVersionHostedAsync("shipping", 3, default)).ShouldBeTrue();

        await registry.PruneAsync(T0.AddMinutes(5), default);
        (await registry.IsVersionHostedAsync("shipping", 3, default)).ShouldBeFalse();
    }

    [TestMethod]
    public async Task IsVersionHosted_reflects_reregistration_with_different_versions()
    {
        IRunnerRegistry registry = await this.NewRegistryAsync();
        await registry.RegisterAsync(Reg("runner-1", T0, T0, hosted: [("shipping", 3, "h", true)]), default);
        (await registry.IsVersionHostedAsync("shipping", 3, default)).ShouldBeTrue();

        // The same runner re-registers hosting a different version — the old (shipping, 3) entry must drop out.
        await registry.RegisterAsync(Reg("runner-1", T0, T0.AddMinutes(1), hosted: [("shipping", 4, "h", true)]), default);
        (await registry.IsVersionHostedAsync("shipping", 3, default)).ShouldBeFalse();
        (await registry.IsVersionHostedAsync("shipping", 4, default)).ShouldBeTrue();
    }

    [TestMethod]
    public async Task Listing_keyset_pages_by_runner_id_without_gaps_or_duplicates()
    {
        IRunnerRegistry registry = await this.NewRegistryAsync();
        string[] ids = ["runner-3", "runner-1", "runner-5", "runner-2", "runner-8", "runner-0", "runner-4", "runner-6"];
        foreach (string id in ids)
        {
            await registry.RegisterAsync(Reg(id, T0, T0), default);
        }

        // Walk every page via the continuation token with a small limit; collect ids in page order. The token is
        // round-tripped through the JsonString seam exactly as the HTTP layer does.
        var seen = new List<string>();
        byte[]? token = null;
        int pages = 0;
        do
        {
            using ParsedJsonDocument<Corvus.Text.Json.Arazzo.Durability.JsonString>? tokenDoc = token is null ? null : AsJsonString(token);
            using RunnerRegistryPage page = await registry.ListAsync(3, tokenDoc?.RootElement ?? default, default);
            page.Runners.Count.ShouldBeLessThanOrEqualTo(3);
            foreach (RunnerRegistration runner in page.Runners)
            {
                seen.Add(runner.RunnerIdValue);
            }

            token = page.NextPageToken.IsEmpty ? null : page.NextPageToken.ToArray();
            pages++;
        }
        while (token is not null);

        // 8 runners, 3 per page → 3 pages; no duplicates or gaps across boundaries; contractual runnerId (ordinal) order.
        pages.ShouldBe(3);
        seen.ShouldBe(ids.OrderBy(x => x, StringComparer.Ordinal).ToArray());

        // A malformed token is rejected (rather than silently restarting from the first page).
        await Should.ThrowAsync<FormatException>(async () =>
        {
            using ParsedJsonDocument<Corvus.Text.Json.Arazzo.Durability.JsonString> badToken = AsJsonString("this~is~not~a~token"u8);
            using RunnerRegistryPage bad = await registry.ListAsync(3, badToken.RootElement, default);
        });
    }

    // Wraps a value's UTF-8 as the JSON string a request carries it as — the conformance feeds a previous page's
    // NextPageToken (the store's emitted bytes) back through the JsonString pageToken seam, mirroring HTTP.
    private static ParsedJsonDocument<Corvus.Text.Json.Arazzo.Durability.JsonString> AsJsonString(ReadOnlySpan<byte> valueUtf8)
    {
        byte[] quoted = new byte[valueUtf8.Length + 2];
        quoted[0] = (byte)'"';
        valueUtf8.CopyTo(quoted.AsSpan(1));
        quoted[^1] = (byte)'"';
        return ParsedJsonDocument<Corvus.Text.Json.Arazzo.Durability.JsonString>.Parse(quoted);
    }

    private static RunnerRegistration Reg(
        string runnerId,
        DateTimeOffset startedAt,
        DateTimeOffset lastSeenAt,
        int maxConcurrency = 4,
        string[]? transports = null,
        (string BaseId, int Version, string Hash, bool Loaded)[]? hosted = null,
        string? address = null,
        string environment = "production")
    {
        string[] runnerTransports = transports ?? ["http"];
        (string BaseId, int Version, string Hash, bool Loaded)[] hostedVersions = hosted ?? [];

        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("runnerId", runnerId);
            writer.WriteString("environment", environment);
            if (address is not null)
            {
                writer.WriteString("address", address);
            }

            writer.WriteString("startedAt", startedAt.ToString("O", CultureInfo.InvariantCulture));
            writer.WriteString("lastSeenAt", lastSeenAt.ToString("O", CultureInfo.InvariantCulture));
            writer.WriteNumber("maxConcurrency", maxConcurrency);

            writer.WriteStartArray("transports");
            foreach (string t in runnerTransports)
            {
                writer.WriteStringValue(t);
            }

            writer.WriteEndArray();

            writer.WriteStartArray("hostedVersions");
            foreach ((string BaseId, int Version, string Hash, bool Loaded) h in hostedVersions)
            {
                writer.WriteStartObject();
                writer.WriteString("baseWorkflowId", h.BaseId);
                writer.WriteNumber("versionNumber", h.Version);
                writer.WriteString("hash", h.Hash);
                writer.WriteBoolean("loaded", h.Loaded);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        using ParsedJsonDocument<RunnerRegistration> doc = ParsedJsonDocument<RunnerRegistration>.Parse(buffer.WrittenMemory);
        return doc.RootElement.Clone();
    }

    private static DateTimeOffset LastSeen(in RunnerRegistration registration)
        => DateTimeOffset.Parse((string)registration.LastSeenAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    private static DateTimeOffset ReadStarted(in RunnerRegistration registration)
        => DateTimeOffset.Parse((string)registration.StartedAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    private async ValueTask<IRunnerRegistry> NewRegistryAsync()
    {
        IRunnerRegistry registry = await this.CreateRegistryAsync();
        if (registry is IAsyncDisposable disposable)
        {
            this.disposables.Add(disposable);
        }

        return registry;
    }
}