// <copyright file="NatsJetStreamObservedIdentityReachPushdownTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.KeyValueStore;
using Shouldly;
using Testcontainers.Nats;

namespace Corvus.Text.Json.Arazzo.Durability.NatsJetStream.Tests;

/// <summary>
/// Proves the NATS observed-identity store narrows a reach-scoped search through its security-label entries
/// (design §14.4) instead of scanning every identity key and discarding documents in process — the
/// observed-identity sibling of <see cref="NatsJetStreamReachPushdownTests"/>.
/// </summary>
/// <remarks>
/// The label entries share the identity bucket (the <c>v.</c>/<c>k.</c> key namespaces), so a consumer created on
/// the bucket's stream can be either a label lookup or a full key scan — the request PAYLOAD tells them apart by
/// the consumer's filter subject (<c>…​.v.</c>-prefixed for a label lookup, the bare <c>&gt;</c> span for a scan),
/// which is why this file's monitor records payloads where the run/catalog tests record only subjects.
/// </remarks>
[TestClass]
[TestCategory("integration")]
[TestCategory("docker")]
public sealed class NatsJetStreamObservedIdentityReachPushdownTests
{
    private static readonly ObservedIdentity.GranteeKind Team = ObservedIdentity.GranteeKind.EnumValues.Team;
    private static readonly ObservedIdentity.GranteeKind AllKinds = default;
    private static NatsContainer container = null!;
    private static NatsConnection connection = null!;

    [ClassInitialize]
    public static async Task ClassInitAsync(TestContext context)
    {
        container = new NatsBuilder().WithImage("nats:2.11").WithCommand("-js").Build();
        await container.StartAsync();
        connection = new NatsConnection(NatsOpts.Default with { Url = container.GetConnectionString() });
    }

    [ClassCleanup]
    public static async Task ClassCleanupAsync()
    {
        if (connection is not null)
        {
            await connection.DisposeAsync();
        }

        if (container is not null)
        {
            await container.DisposeAsync();
        }
    }

    [TestMethod]
    public async Task A_reach_scoped_search_resolves_the_label_entries_and_reads_only_those_identities()
    {
        IObservedIdentityStore store = await NewStoreAsync();
        for (int i = 0; i < 20; ++i)
        {
            await store.SeenAsync(Team, Str($"acme-{i:D3}"), default, Tags("acme"), true, "test", default);
            await store.SeenAsync(Team, Str($"globex-{i:D3}"), default, Tags("globex"), true, "test", default);
        }

        await using ApiLog log = await ApiLog.StartAsync(container.GetConnectionString());

        // The reachable identities (globex-*) do NOT lead in subject order — acme-* sorts first — so a store
        // that merely scanned and took the first matching page could not satisfy the read bound by accident.
        using ObservedIdentityPage page = await store.SearchAsync(ScopeBy("tenant", "globex"), AllKinds, Str(string.Empty), 5, default, default);
        List<(string Subject, string Payload)> traffic = await log.FinishAsync();

        page.Identities.Count.ShouldBe(5);
        page.Identities.ShouldAllBe(i => i.SubjectValueValue.StartsWith("globex-", StringComparison.Ordinal));

        // The label entries were consulted: consumers filtered to the v./k. namespaces...
        traffic.ShouldContain(t => IsLabelLookup(t));

        // ...the identity keys were never scanned wholesale...
        traffic.ShouldNotContain(t => IsFullScan(t));

        // ...and only candidate documents were read. A scan would have read the 20 acme documents too.
        traffic.Count(IsIdentityRead).ShouldBeLessThanOrEqualTo(20);
    }

    [TestMethod]
    public async Task An_unreachable_search_reads_no_identities_at_all()
    {
        IObservedIdentityStore store = await NewStoreAsync();
        await store.SeenAsync(Team, Str("acme-team"), default, Tags("acme"), true, "test", default);

        await using ApiLog log = await ApiLog.StartAsync(container.GetConnectionString());
        using ObservedIdentityPage page = await store.SearchAsync(ScopeBy("tenant", "nobody"), AllKinds, Str(string.Empty), 5, default, default);
        List<(string Subject, string Payload)> traffic = await log.FinishAsync();

        page.Identities.Count.ShouldBe(0);

        // An empty candidate set is not "no narrowing": the store must answer without reading a single document.
        traffic.ShouldNotContain(t => IsIdentityRead(t));
        traffic.ShouldNotContain(t => IsFullScan(t));
    }

    [TestMethod]
    public async Task An_unrestricted_search_still_scans_rather_than_enumerating_labels()
    {
        // The negative control: narrowing must be driven by the reach, not applied always.
        IObservedIdentityStore store = await NewStoreAsync();
        await store.SeenAsync(Team, Str("acme-team"), default, Tags("acme"), true, "test", default);

        await using ApiLog log = await ApiLog.StartAsync(container.GetConnectionString());
        using ObservedIdentityPage page = await store.SearchAsync(AccessContext.System, AllKinds, Str(string.Empty), 5, default, default);
        List<(string Subject, string Payload)> traffic = await log.FinishAsync();

        traffic.ShouldContain(t => IsFullScan(t));
        traffic.ShouldNotContain(t => IsLabelLookup(t));
    }

    [TestMethod]
    public async Task A_re_sighting_that_changes_the_tags_moves_the_label_entries()
    {
        // The wire half of the conformance suite's visibility test: after the re-homing, the old tenant's search
        // answers empty from the label entries alone, without reading the document the identity still occupies —
        // a stale entry would produce the same empty page while still costing the read.
        IObservedIdentityStore store = await NewStoreAsync();
        await store.SeenAsync(Team, Str("mobile"), default, Tags("acme"), true, "test", default);
        await store.SeenAsync(Team, Str("mobile"), default, Tags("globex"), true, "test", default);

        using (ObservedIdentityPage globex = await store.SearchAsync(ScopeBy("tenant", "globex"), AllKinds, Str(string.Empty), 5, default, default))
        {
            globex.Identities.Count.ShouldBe(1);
        }

        await using ApiLog log = await ApiLog.StartAsync(container.GetConnectionString());
        using (ObservedIdentityPage acme = await store.SearchAsync(ScopeBy("tenant", "acme"), AllKinds, Str(string.Empty), 5, default, default))
        {
            acme.Identities.Count.ShouldBe(0);
        }

        (await log.FinishAsync()).ShouldNotContain(t => IsIdentityRead(t));
    }

    [TestMethod]
    public async Task A_tag_key_sharing_a_prefix_cannot_widen_another_tags_reach()
    {
        // Each key/value part is its own encoded subject token, which is what keeps the entry key injective —
        // an index widening is invisible in the results (the exact evaluation discards it), so the wire is what
        // the test observes.
        IObservedIdentityStore store = await NewStoreAsync();
        await store.SeenAsync(Team, Str("one"), default, SecurityTagSet.FromTags([new SecurityTag("a", "bc")]), true, "test", default);
        await store.SeenAsync(Team, Str("two"), default, SecurityTagSet.FromTags([new SecurityTag("ab", "c")]), true, "test", default);

        await using ApiLog log = await ApiLog.StartAsync(container.GetConnectionString());
        using ObservedIdentityPage page = await store.SearchAsync(ScopeBy("a", "bc"), AllKinds, Str(string.Empty), 5, default, default);
        List<(string Subject, string Payload)> traffic = await log.FinishAsync();

        page.Identities.Single().SubjectValueValue.ShouldBe("one");
        traffic.Count(IsIdentityRead).ShouldBe(1);
    }

    [TestMethod]
    public async Task A_tag_value_carrying_subject_structure_characters_round_trips()
    {
        IObservedIdentityStore store = await NewStoreAsync();
        await store.SeenAsync(Team, Str("odd"), default, SecurityTagSet.FromTags([new SecurityTag("tenant", "a.b*c>d")]), true, "test", default);

        using ObservedIdentityPage page = await store.SearchAsync(ScopeBy("tenant", "a.b*c>d"), AllKinds, Str(string.Empty), 5, default, default);
        page.Identities.Count.ShouldBe(1);
    }

    private static bool IsIdentityRead((string Subject, string Payload) t)
        => t.Subject is "$JS.API.DIRECT.GET.KV_arazzo_observed_identities" or "$JS.API.STREAM.MSG.GET.KV_arazzo_observed_identities"
            || (t.Subject.StartsWith("$JS.API.DIRECT.GET.KV_arazzo_observed_identities.", StringComparison.Ordinal)
                || t.Subject.StartsWith("$JS.API.STREAM.MSG.GET.KV_arazzo_observed_identities.", StringComparison.Ordinal))

            // Only the oid. document namespace counts as an identity read; the digest/label maintenance gets are
            // addressed under their own namespaces and never occur on the search path anyway.
            && !t.Payload.Contains(".digof.", StringComparison.Ordinal);

    // An unfiltered key listing's consumer carries no per-namespace filter subject in its create request, so the
    // scan is recognised as "a consumer on the bucket that is NOT one of the namespace-filtered lookups".
    private static bool IsFullScan((string Subject, string Payload) t)
        => t.Subject.StartsWith("$JS.API.CONSUMER.CREATE.KV_arazzo_observed_identities.", StringComparison.Ordinal)
            && !t.Payload.Contains("$KV.arazzo_observed_identities.v.", StringComparison.Ordinal)
            && !t.Payload.Contains("$KV.arazzo_observed_identities.k.", StringComparison.Ordinal)
            && !t.Payload.Contains("$KV.arazzo_observed_identities.digx.", StringComparison.Ordinal);

    private static bool IsLabelLookup((string Subject, string Payload) t)
        => t.Subject.StartsWith("$JS.API.CONSUMER.CREATE.KV_arazzo_observed_identities.", StringComparison.Ordinal)
            && (t.Payload.Contains("$KV.arazzo_observed_identities.v.", StringComparison.Ordinal)
                || t.Payload.Contains("$KV.arazzo_observed_identities.k.", StringComparison.Ordinal));

    private static SecurityTagSet Tags(string tenant) => SecurityTagSet.FromTags([new SecurityTag("tenant", tenant)]);

    private static JsonString Str(string value) => JsonString.ParseValue($"\"{value}\"");

    private static AccessContext ScopeBy(string key, string value) => AccessContext.Uniform(
        new SecurityFilter([SecurityRule.Compile($"{key} == $claim.{key}")], new Dictionary<string, IReadOnlyList<string>> { [key] = [value] }));

    private static async ValueTask<IObservedIdentityStore> NewStoreAsync()
    {
        var kv = new NatsKVContext(new NatsJSContext(connection));
        await NatsKvTestReset.ResetAndProvisionAsync(
            kv,
            ["arazzo_observed_identities"],
            () => NatsJetStreamObservedIdentityStore.PrepareAsync(connection));
        return await NatsJetStreamObservedIdentityStore.ConnectAsync(connection);
    }

    // Observes the store's JetStream API traffic from a second connection, recording subject AND payload — the
    // payload carries a consumer's filter subject, which is what tells a label lookup from a full key scan when
    // both live on the same bucket. FinishAsync drains briefly so absence assertions are not satisfied by racing
    // the subscription.
    private sealed class ApiLog : IAsyncDisposable
    {
        private readonly NatsConnection monitor;
        private readonly CancellationTokenSource cts = new();
        private readonly List<(string Subject, string Payload)> traffic = [];
        private readonly TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private Task? pump;

        private ApiLog(NatsConnection monitor) => this.monitor = monitor;

        public static async ValueTask<ApiLog> StartAsync(string url)
        {
            var log = new ApiLog(new NatsConnection(NatsOpts.Default with { Url = url }));
            log.pump = Task.Run(log.PumpAsync);
            await log.ready.Task;
            return log;
        }

        public async ValueTask<List<(string Subject, string Payload)>> FinishAsync()
        {
            await Task.Delay(TimeSpan.FromMilliseconds(300));
            lock (this.traffic)
            {
                return [.. this.traffic];
            }
        }

        public async ValueTask DisposeAsync()
        {
            await this.cts.CancelAsync();
            if (this.pump is { } task)
            {
                try
                {
                    await task;
                }
                catch (OperationCanceledException)
                {
                }
            }

            await this.monitor.DisposeAsync();
            this.cts.Dispose();
        }

        private async Task PumpAsync()
        {
            await using INatsSub<byte[]> sub = await this.monitor.SubscribeCoreAsync<byte[]>("$JS.API.>", cancellationToken: this.cts.Token);
            this.ready.SetResult();
            await foreach (NatsMsg<byte[]> msg in sub.Msgs.ReadAllAsync(this.cts.Token))
            {
                string payload = msg.Data is { } data ? Encoding.UTF8.GetString(data) : string.Empty;
                lock (this.traffic)
                {
                    this.traffic.Add((msg.Subject, payload));
                }
            }
        }
    }
}
