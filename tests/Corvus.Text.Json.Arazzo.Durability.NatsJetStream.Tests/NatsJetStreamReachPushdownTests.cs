// <copyright file="NatsJetStreamReachPushdownTests.cs" company="Endjin Limited">
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
/// Proves the NATS store narrows a reach-filtered query through its security-label bucket (design §14.4) —
/// resolved by subject-filtered key listings — instead of scanning every run key and discarding envelopes in
/// process.
/// </summary>
/// <remarks>
/// Every JetStream operation is itself a NATS request on a <c>$JS.API.&gt;</c> subject, so a second connection
/// subscribed there sees the store's actual traffic. The subject mix discriminates: a per-run envelope read is
/// a <c>DIRECT.GET</c> (or <c>STREAM.MSG.GET</c>) on the runs bucket's stream, a key scan creates a consumer on
/// it, and the label resolution creates consumers on the labels bucket's stream instead.
/// </remarks>
[TestClass]
[TestCategory("integration")]
[TestCategory("docker")]
public sealed class NatsJetStreamReachPushdownTests
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
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
    public async Task A_reach_filtered_query_resolves_the_label_bucket_and_reads_only_those_runs()
    {
        IWorkflowStateStore store = await NewStoreAsync();
        await SeedAsync(store, count: 40);

        await using ApiLog log = await ApiLog.StartAsync(container.GetConnectionString());

        // The reachable runs (globex-*) do NOT lead in id order — acme-* sorts first — so a store that merely
        // swept and took the first matching page would read the acme envelopes on the way and the read bound
        // below could not hold by accident.
        WorkflowRunPage page = await ((IWorkflowWaitIndex)store).QueryAsync(
            new WorkflowQuery(Limit: 5, Security: GlobexReach()), default);
        List<string> subjects = await log.FinishAsync();

        page.Runs.Count.ShouldBe(5);
        page.Runs.ShouldAllBe(r => r.Id.Value.StartsWith("globex-", StringComparison.Ordinal));

        // The label bucket was consulted: the reach became subject-filtered key listings there...
        subjects.ShouldContain(s => IsLabelTraffic(s));

        // ...the run keys were never scanned...
        subjects.ShouldNotContain(s => IsRunScan(s));

        // ...and only candidate envelopes were read. A sweep would have read the 20 acme envelopes too.
        subjects.Count(IsRunRead).ShouldBeLessThanOrEqualTo(20);
    }

    [TestMethod]
    public async Task An_unreachable_query_reads_no_runs_at_all()
    {
        IWorkflowStateStore store = await NewStoreAsync();
        await SeedAsync(store, count: 8);

        await using ApiLog log = await ApiLog.StartAsync(container.GetConnectionString());
        WorkflowRunPage page = await ((IWorkflowWaitIndex)store).QueryAsync(
            new WorkflowQuery(Limit: 5, Security: Reach("tenant == 'nobody'")), default);
        List<string> subjects = await log.FinishAsync();

        page.Runs.ShouldBeEmpty();

        // An empty candidate set is not "no narrowing": the store must answer without touching the runs bucket.
        subjects.ShouldNotContain(s => IsRunRead(s));
        subjects.ShouldNotContain(s => IsRunScan(s));
    }

    [TestMethod]
    public async Task An_unfiltered_query_still_scans_rather_than_enumerating_labels()
    {
        // The negative control for the first test: narrowing must be driven by the reach, not applied always.
        IWorkflowStateStore store = await NewStoreAsync();
        await SeedAsync(store, count: 8);

        await using ApiLog log = await ApiLog.StartAsync(container.GetConnectionString());
        await ((IWorkflowWaitIndex)store).QueryAsync(new WorkflowQuery(Limit: 5), default);
        List<string> subjects = await log.FinishAsync();

        subjects.ShouldContain(s => IsRunScan(s));
        subjects.ShouldNotContain(s => IsLabelTraffic(s));
    }

    [TestMethod]
    public async Task Deleting_a_run_removes_its_label_entries()
    {
        IWorkflowStateStore store = await NewStoreAsync();
        var index = (IWorkflowWaitIndex)store;

        await store.SaveAsync(A("acme-0"), Bytes(), Secured([new("tenant", "acme")]), WorkflowEtag.None, default);
        (await index.QueryAsync(new WorkflowQuery(Limit: 10, Security: AcmeReach()), default)).Runs.Count.ShouldBe(1);

        await store.DeleteAsync(A("acme-0"), default);

        // A run recreated under the same id must not inherit the deleted run's labels: after the delete the acme
        // entry is gone, so an acme-reach query answers empty from the label bucket alone, without reading the
        // envelope the recreated globex run now occupies.
        await store.SaveAsync(A("acme-0"), Bytes(), Secured([new("tenant", "globex")]), WorkflowEtag.None, default);

        await using ApiLog log = await ApiLog.StartAsync(container.GetConnectionString());
        (await index.QueryAsync(new WorkflowQuery(Limit: 10, Security: AcmeReach()), default)).Runs.ShouldBeEmpty();
        (await log.FinishAsync()).ShouldNotContain(s => IsRunRead(s));
    }

    [TestMethod]
    public async Task Changing_a_runs_tags_on_save_moves_its_label_entries()
    {
        // The save path diffs the desired entries against the run's current ones (a reverse subject lookup on
        // its id token) and moves only the difference. Both directions must hold: the new tenant's entry added
        // (or the run is hidden from its rightful reach, an availability failure) and the old tenant's removed.
        IWorkflowStateStore store = await NewStoreAsync();
        var index = (IWorkflowWaitIndex)store;

        WorkflowEtag first = await store.SaveAsync(A("retag-0"), Bytes(), Secured([new("tenant", "acme")]), WorkflowEtag.None, default);
        await store.SaveAsync(A("retag-0"), Bytes(), Secured([new("tenant", "globex")]), first, default);

        (await index.QueryAsync(new WorkflowQuery(Limit: 10, Security: GlobexReach()), default)).Runs.Count.ShouldBe(1);

        await using ApiLog log = await ApiLog.StartAsync(container.GetConnectionString());
        (await index.QueryAsync(new WorkflowQuery(Limit: 10, Security: AcmeReach()), default)).Runs.ShouldBeEmpty();
        (await log.FinishAsync()).ShouldNotContain(s => IsRunRead(s));
    }

    [TestMethod]
    public async Task A_tag_key_sharing_a_prefix_cannot_widen_another_tags_reach()
    {
        // Each key/value part is its own encoded subject token, which is what keeps the entry key injective:
        // were the parts concatenated raw, ("a", "bc") and ("ab", "c") would share an entry shape, and tagging a
        // row with one would put it in the candidates of a rule about the other — invisible in the results (the
        // exact evaluation discards it), so the wire is what the test observes.
        IWorkflowStateStore store = await NewStoreAsync();
        var index = (IWorkflowWaitIndex)store;

        await store.SaveAsync(A("one"), Bytes(), Secured([new("a", "bc")]), WorkflowEtag.None, default);
        await store.SaveAsync(A("two"), Bytes(), Secured([new("ab", "c")]), WorkflowEtag.None, default);

        await using ApiLog log = await ApiLog.StartAsync(container.GetConnectionString());
        WorkflowRunPage page = await index.QueryAsync(new WorkflowQuery(Limit: 10, Security: Reach("a == 'bc'")), default);
        List<string> subjects = await log.FinishAsync();

        page.Runs.Count.ShouldBe(1);
        page.Runs[0].Id.Value.ShouldBe("one");
        subjects.Count(IsRunRead).ShouldBe(1);
    }

    [TestMethod]
    public async Task A_tag_value_carrying_subject_structure_characters_round_trips()
    {
        // A KV key is a subject, so '.', '*' and '>' in a tag would otherwise BE subject structure — the token
        // encoding is what stops a crafted tag from being rejected by the server or matching another label's
        // entries as a wildcard.
        IWorkflowStateStore store = await NewStoreAsync();
        var index = (IWorkflowWaitIndex)store;

        await store.SaveAsync(A("odd-0"), Bytes(), Secured([new("tenant", "a.b*c>d")]), WorkflowEtag.None, default);

        (await index.QueryAsync(new WorkflowQuery(Limit: 10, Security: Reach("tenant == 'a.b*c>d'")), default))
            .Runs.Count.ShouldBe(1);
    }

    private static bool IsRunRead(string subject)
        => subject.StartsWith("$JS.API.DIRECT.GET.KV_arazzo_runs", StringComparison.Ordinal)
            || subject.StartsWith("$JS.API.STREAM.MSG.GET.KV_arazzo_runs", StringComparison.Ordinal);

    private static bool IsRunScan(string subject)
        => subject.StartsWith("$JS.API.CONSUMER.CREATE.KV_arazzo_runs", StringComparison.Ordinal);

    private static bool IsLabelTraffic(string subject)
        => subject.Contains("KV_arazzo_labels", StringComparison.Ordinal);

    private static SecurityFilter AcmeReach() => Reach("tenant == 'acme'");

    private static SecurityFilter GlobexReach() => Reach("tenant == 'globex'");

    private static SecurityFilter Reach(string rule)
        => new([SecurityRule.Compile(rule)], new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal));

    private static ReadOnlyMemory<byte> Bytes() => Encoding.UTF8.GetBytes("{}");

    private static WorkflowRunIndexEntry Secured(SecurityTag[] securityTags)
        => new("wf", WorkflowRunStatus.Running, T0, T0, SecurityTags: SecurityTagSet.FromTags(securityTags));

    // The run's full (environment, runId) address (ADR 0065 decision 9).
    private static WorkflowRunAddress A(string runId) => new("development", new WorkflowRunId(runId));

    // Ids are prefixed by tenant so the globex runs a reach test asks for do NOT lead in id order.
    private static async ValueTask SeedAsync(IWorkflowStateStore store, int count)
    {
        for (int i = 0; i < count / 2; ++i)
        {
            await store.SaveAsync(A($"acme-{i:D3}"), Bytes(), Secured([new("tenant", "acme")]), WorkflowEtag.None, default);
            await store.SaveAsync(A($"globex-{i:D3}"), Bytes(), Secured([new("tenant", "globex")]), WorkflowEtag.None, default);
        }
    }

    private static async ValueTask<IWorkflowStateStore> NewStoreAsync()
    {
        var kv = new NatsKVContext(new NatsJSContext(connection));
        await NatsKvTestReset.ResetAndProvisionAsync(
            kv,
            ["arazzo_runs", "arazzo_leases", "arazzo_labels"],
            () => NatsJetStreamWorkflowStateStore.PrepareAsync(connection));
        return await NatsJetStreamWorkflowStateStore.ConnectAsync(connection);
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
