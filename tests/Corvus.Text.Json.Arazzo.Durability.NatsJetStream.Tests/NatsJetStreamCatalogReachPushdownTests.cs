// <copyright file="NatsJetStreamCatalogReachPushdownTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json.Arazzo.Durability;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.KeyValueStore;
using Shouldly;
using Testcontainers.Nats;

namespace Corvus.Text.Json.Arazzo.Durability.NatsJetStream.Tests;

/// <summary>
/// Proves the NATS catalog store narrows a reach-filtered query through its security-label bucket (design
/// §14.4) instead of scanning every version key and discarding envelopes in process. The catalog sibling of
/// <see cref="NatsJetStreamReachPushdownTests"/>.
/// </summary>
/// <remarks>
/// The subject mix discriminates, observed from a second connection on <c>$JS.API.&gt;</c>: a per-version
/// envelope read addresses the catalog bucket's stream, a key scan creates a consumer on it, and the label
/// resolution creates consumers on the labels bucket's stream instead. The catalog stream's name is a prefix of
/// the labels stream's, so the read/scan matchers require the token boundary after it.
/// </remarks>
[TestClass]
[TestCategory("integration")]
[TestCategory("docker")]
public sealed class NatsJetStreamCatalogReachPushdownTests
{
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
    public async Task A_reach_filtered_query_resolves_the_label_bucket_and_reads_only_those_versions()
    {
        IWorkflowCatalogStore store = await NewStoreAsync();
        await SeedAsync(store, count: 40);

        await using ApiLog log = await ApiLog.StartAsync(container.GetConnectionString());

        // The reachable versions (globex-*) do NOT lead in sort order — acme-* sorts first — so a store that
        // merely swept and took the first matching page would read the acme envelopes on the way and the read
        // bound below could not hold by accident.
        using CatalogPage page = await store.QueryAsync(new CatalogQuery(Limit: 5, Security: GlobexReach()), default);
        List<string> subjects = await log.FinishAsync();

        page.Versions.Count.ShouldBe(5);
        page.Versions.ShouldAllBe(v => v.Ref.BaseWorkflowId.StartsWith("globex-", StringComparison.Ordinal));

        // The label bucket was consulted: the reach became subject-filtered key listings there...
        subjects.ShouldContain(s => IsLabelTraffic(s));

        // ...the catalog keys were never scanned...
        subjects.ShouldNotContain(s => IsCatalogScan(s));

        // ...and only candidate envelopes were read. A sweep would have read the 20 acme envelopes too.
        subjects.Count(IsCatalogRead).ShouldBeLessThanOrEqualTo(20);
    }

    [TestMethod]
    public async Task An_unreachable_query_reads_no_versions_at_all()
    {
        IWorkflowCatalogStore store = await NewStoreAsync();
        await SeedAsync(store, count: 8);

        await using ApiLog log = await ApiLog.StartAsync(container.GetConnectionString());
        using CatalogPage page = await store.QueryAsync(new CatalogQuery(Limit: 5, Security: Reach("tenant == 'nobody'")), default);
        List<string> subjects = await log.FinishAsync();

        page.Versions.ShouldBeEmpty();

        // An empty candidate set is not "no narrowing": the store must answer without touching the catalog bucket.
        subjects.ShouldNotContain(s => IsCatalogRead(s));
        subjects.ShouldNotContain(s => IsCatalogScan(s));
    }

    [TestMethod]
    public async Task An_unfiltered_query_still_scans_rather_than_enumerating_labels()
    {
        // The negative control for the first test: narrowing must be driven by the reach, not applied always.
        IWorkflowCatalogStore store = await NewStoreAsync();
        await SeedAsync(store, count: 8);

        await using ApiLog log = await ApiLog.StartAsync(container.GetConnectionString());
        using CatalogPage page = await store.QueryAsync(new CatalogQuery(Limit: 5), default);
        List<string> subjects = await log.FinishAsync();

        subjects.ShouldContain(s => IsCatalogScan(s));
        subjects.ShouldNotContain(s => IsLabelTraffic(s));
    }

    [TestMethod]
    public async Task Deleting_a_version_removes_its_label_entries()
    {
        IWorkflowCatalogStore store = await NewStoreAsync();

        (await store.AddAsync("secure-flow", Package("secure-flow"), Meta("acme"), default)).Dispose();
        using (CatalogPage page = await store.QueryAsync(new CatalogQuery(Limit: 10, Security: AcmeReach()), default))
        {
            page.Versions.Count.ShouldBe(1);
        }

        (await store.DeleteAsync("secure-flow", 1, default)).ShouldBeTrue();

        // A version recreated under the same (base, number) must not inherit the deleted version's labels: after
        // the delete the acme entry is gone, so an acme-reach query answers empty from the label bucket alone,
        // without reading the envelope the recreated globex version now occupies.
        (await store.AddAsync("secure-flow", Package("secure-flow"), Meta("globex"), default)).Dispose();

        await using ApiLog log = await ApiLog.StartAsync(container.GetConnectionString());
        using (CatalogPage page = await store.QueryAsync(new CatalogQuery(Limit: 10, Security: AcmeReach()), default))
        {
            page.Versions.ShouldBeEmpty();
        }

        (await log.FinishAsync()).ShouldNotContain(s => IsCatalogRead(s));
    }

    [TestMethod]
    public async Task Retagging_a_version_moves_its_label_entries()
    {
        // A §14.2 re-tag replaces the version's security tags in place, so the label diff must be maintained —
        // the new tenant's entry added (or the version is hidden from its rightful reach, an availability
        // failure) and the old tenant's removed.
        IWorkflowCatalogStore store = await NewStoreAsync();

        (await store.AddAsync("retag-flow", Package("retag-flow"), Meta("acme"), default)).Dispose();
        using (ParsedJsonDocument<CatalogVersion>? updated = await store.UpdateMetadataAsync(
            "retag-flow", 1, new CatalogMetadataPatch("bob", SecurityTags: SecurityTagSet.FromTags([new("tenant", "globex")])), default))
        {
            updated.ShouldNotBeNull();
        }

        using (CatalogPage page = await store.QueryAsync(new CatalogQuery(Limit: 10, Security: GlobexReach()), default))
        {
            page.Versions.Count.ShouldBe(1);
        }

        await using ApiLog log = await ApiLog.StartAsync(container.GetConnectionString());
        using (CatalogPage page = await store.QueryAsync(new CatalogQuery(Limit: 10, Security: AcmeReach()), default))
        {
            page.Versions.ShouldBeEmpty();
        }

        (await log.FinishAsync()).ShouldNotContain(s => IsCatalogRead(s));
    }

    [TestMethod]
    public async Task A_tag_key_sharing_a_prefix_cannot_widen_another_tags_reach()
    {
        // Each key/value part is its own encoded subject token, which is what keeps the entry key injective:
        // were the parts concatenated raw, ("a", "bc") and ("ab", "c") would share an entry shape — an index
        // widening invisible in the results (the exact evaluation discards it), so the wire is what the test
        // observes.
        IWorkflowCatalogStore store = await NewStoreAsync();

        (await store.AddAsync("one", Package("one"), MetaTags([new("a", "bc")]), default)).Dispose();
        (await store.AddAsync("two", Package("two"), MetaTags([new("ab", "c")]), default)).Dispose();

        await using ApiLog log = await ApiLog.StartAsync(container.GetConnectionString());
        using CatalogPage page = await store.QueryAsync(new CatalogQuery(Limit: 10, Security: Reach("a == 'bc'")), default);
        List<string> subjects = await log.FinishAsync();

        page.Versions.Count.ShouldBe(1);
        page.Versions[0].Ref.BaseWorkflowId.ShouldBe("one");
        subjects.Count(IsCatalogRead).ShouldBe(1);
    }

    [TestMethod]
    public async Task A_tag_value_carrying_subject_structure_characters_round_trips()
    {
        // A KV key is a subject, so '.', '*' and '>' in a tag would otherwise BE subject structure — the token
        // encoding is what stops a crafted tag from being rejected by the server or matching another label's
        // entries as a wildcard.
        IWorkflowCatalogStore store = await NewStoreAsync();

        (await store.AddAsync("odd-flow", Package("odd-flow"), MetaTags([new("tenant", "a.b*c>d")]), default)).Dispose();

        using CatalogPage page = await store.QueryAsync(new CatalogQuery(Limit: 10, Security: Reach("tenant == 'a.b*c>d'")), default);
        page.Versions.Count.ShouldBe(1);
    }

    // The catalog stream's name is a prefix of the labels stream's, so require the token boundary: a bare
    // "$JS.API.DIRECT.GET.KV_arazzo_catalog" (payload-form direct get) or any subject continuing with '.',
    // which a labels-stream subject never does ('_' follows instead).
    private static bool IsCatalogRead(string subject)
        => subject is "$JS.API.DIRECT.GET.KV_arazzo_catalog" or "$JS.API.STREAM.MSG.GET.KV_arazzo_catalog"
            || subject.StartsWith("$JS.API.DIRECT.GET.KV_arazzo_catalog.", StringComparison.Ordinal)
            || subject.StartsWith("$JS.API.STREAM.MSG.GET.KV_arazzo_catalog.", StringComparison.Ordinal);

    private static bool IsCatalogScan(string subject)
        => subject.StartsWith("$JS.API.CONSUMER.CREATE.KV_arazzo_catalog.", StringComparison.Ordinal);

    private static bool IsLabelTraffic(string subject)
        => subject.Contains("KV_arazzo_catalog_labels", StringComparison.Ordinal);

    private static SecurityFilter AcmeReach() => Reach("tenant == 'acme'");

    private static SecurityFilter GlobexReach() => Reach("tenant == 'globex'");

    private static SecurityFilter Reach(string rule)
        => new([SecurityRule.Compile(rule)], new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal));

    private static CatalogMetadata Meta(string tenant)
        => MetaTags([new("tenant", tenant)]);

    private static CatalogMetadata MetaTags(SecurityTag[] tags)
        => new(new CatalogOwner("Team A", "team-a@example.com"), "alice", default, SecurityTagSet.FromTags(tags));

    private static ReadOnlyMemory<byte> Package(string workflowId)
    {
        byte[] workflow = Encoding.UTF8.GetBytes($$"""
        {
          "arazzo": "1.1.0",
          "info": { "title": "Reach Pushdown", "description": "Proves the catalog narrows." },
          "sourceDescriptions": [ { "name": "petstore", "url": "./petstore.json", "type": "openapi" } ],
          "workflows": [ { "workflowId": "{{workflowId}}", "steps": [] } ]
        }
        """);
        byte[] petstore = Encoding.UTF8.GetBytes("""{"openapi":"3.1.0","info":{"title":"Petstore","version":"1.0.0"}}""");
        return CatalogPackage.Build(workflow, [new KeyValuePair<string, byte[]>("petstore", petstore)]);
    }

    // Bases are prefixed by tenant so the globex versions a reach test asks for do NOT lead in sort order.
    private static async ValueTask SeedAsync(IWorkflowCatalogStore store, int count)
    {
        for (int i = 0; i < count / 2; ++i)
        {
            (await store.AddAsync($"acme-{i:D3}", Package($"acme-{i:D3}"), Meta("acme"), default)).Dispose();
            (await store.AddAsync($"globex-{i:D3}", Package($"globex-{i:D3}"), Meta("globex"), default)).Dispose();
        }
    }

    private static async ValueTask<IWorkflowCatalogStore> NewStoreAsync()
    {
        var kv = new NatsKVContext(new NatsJSContext(connection));
        await NatsKvTestReset.ResetAndProvisionAsync(
            kv,
            ["arazzo_catalog", "arazzo_catalog_labels"],
            () => NatsJetStreamWorkflowCatalogStore.PrepareAsync(connection));
        return await NatsJetStreamWorkflowCatalogStore.ConnectAsync(connection);
    }

    // Observes the store's JetStream API traffic from a second connection: every JetStream operation is a NATS
    // request on a $JS.API.> subject, so the subjects seen here are the store's actual wire behaviour. Starting
    // the log after seeding scopes it to the query under test; FinishAsync drains briefly so absence assertions
    // are not satisfied by racing the subscription.
    private sealed class ApiLog : IAsyncDisposable
    {
        private readonly NatsConnection monitor;
        private readonly CancellationTokenSource cts = new();
        private readonly List<string> subjects = [];
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

        public async ValueTask<List<string>> FinishAsync()
        {
            // Cross-connection observation is asynchronous; a short drain keeps "nothing was read" assertions
            // from passing because the messages had not arrived yet.
            await Task.Delay(TimeSpan.FromMilliseconds(300));
            lock (this.subjects)
            {
                return [.. this.subjects];
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
                lock (this.subjects)
                {
                    this.subjects.Add(msg.Subject);
                }
            }
        }
    }
}
