// <copyright file="RedisReachPushdownTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json.Arazzo.Durability;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;
using StackExchange.Redis;
using StackExchange.Redis.Profiling;
using Testcontainers.Redis;

namespace Corvus.Text.Json.Arazzo.Durability.Redis.Tests;

/// <summary>
/// Proves the Redis store narrows a reach-filtered query through its security-label sets (design §14.4) —
/// resolved server-side in one Lua evaluation — instead of sweeping the all-runs set and discarding hashes in
/// process.
/// </summary>
/// <remarks>
/// Redis has no query language to observe, so these tests assert on the commands the store actually issues,
/// captured with the client's profiling API: a narrowed query resolves with one script evaluation and then reads
/// only candidate hashes (never the all-runs SMEMBERS sweep), and an unreachable query reads nothing at all.
/// The command mix discriminates: HGETALL counts how many run hashes were read, SMEMBERS marks a sweep, and
/// EVAL/EVALSHA marks the label resolution.
/// </remarks>
[TestClass]
[TestCategory("integration")]
[TestCategory("docker")]
public sealed class RedisReachPushdownTests
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly List<ConnectionMultiplexer> Connections = [];
    private static RedisContainer container = null!;

    [ClassInitialize]
    public static async Task ClassInitAsync(TestContext context)
    {
        container = new RedisBuilder().WithImage("redis:7-alpine").Build();
        await container.StartAsync();
    }

    [ClassCleanup]
    public static async Task ClassCleanupAsync()
    {
        foreach (ConnectionMultiplexer connection in Connections)
        {
            await connection.DisposeAsync();
        }

        if (container is not null)
        {
            await container.DisposeAsync();
        }
    }

    [TestMethod]
    public async Task A_reach_filtered_query_resolves_the_label_sets_and_reads_only_those_runs()
    {
        (IWorkflowStateStore store, CommandLog log) = await NewStoreAsync();
        await SeedAsync(store, count: 40);

        log.Start();

        // The reachable runs (globex-*) do NOT lead in id order — acme-* sorts first — so a store that merely
        // swept and took the first matching page would read the acme hashes on the way and the HGETALL bound
        // below could not hold by accident.
        WorkflowRunPage page = await ((IWorkflowWaitIndex)store).QueryAsync(
            new WorkflowQuery(Limit: 5, Security: GlobexReach()), default);
        List<string> commands = log.Finish();

        page.Runs.Count.ShouldBe(5);
        page.Runs.ShouldAllBe(r => r.Id.Value.StartsWith("globex-", StringComparison.Ordinal));

        // The label sets were consulted server-side: the reach became one script evaluation...
        commands.ShouldContain(c => c == "EVAL" || c == "EVALSHA");

        // ...the all-runs set was never swept...
        commands.ShouldNotContain("SMEMBERS");

        // ...and only candidate hashes were read. A sweep would have read the 20 acme hashes too.
        commands.Count(c => c == "HGETALL").ShouldBeLessThanOrEqualTo(20);
    }

    [TestMethod]
    public async Task An_unreachable_query_reads_no_runs_at_all()
    {
        (IWorkflowStateStore store, CommandLog log) = await NewStoreAsync();
        await SeedAsync(store, count: 8);

        log.Start();
        WorkflowRunPage page = await ((IWorkflowWaitIndex)store).QueryAsync(
            new WorkflowQuery(Limit: 5, Security: Reach("tenant == 'nobody'")), default);
        List<string> commands = log.Finish();

        page.Runs.ShouldBeEmpty();

        // An empty candidate set is not "no narrowing": the store must answer without reading a single hash.
        commands.ShouldNotContain("HGETALL");
        commands.ShouldNotContain("SMEMBERS");
    }

    [TestMethod]
    public async Task An_unfiltered_query_still_sweeps_rather_than_enumerating_label_sets()
    {
        // The negative control for the first test: narrowing must be driven by the reach, not applied always.
        (IWorkflowStateStore store, CommandLog log) = await NewStoreAsync();
        await SeedAsync(store, count: 8);

        log.Start();
        await ((IWorkflowWaitIndex)store).QueryAsync(new WorkflowQuery(Limit: 5), default);
        List<string> commands = log.Finish();

        commands.ShouldContain("SMEMBERS");
        commands.ShouldNotContain(c => c == "EVAL" || c == "EVALSHA");
    }

    [TestMethod]
    public async Task Deleting_a_run_removes_its_label_entries()
    {
        (IWorkflowStateStore store, CommandLog log) = await NewStoreAsync();
        var index = (IWorkflowWaitIndex)store;

        await store.SaveAsync("acme-0", Bytes(), Secured([new("tenant", "acme")]), WorkflowEtag.None, default);
        (await index.QueryAsync(new WorkflowQuery(Limit: 10, Security: AcmeReach()), default)).Runs.Count.ShouldBe(1);

        await store.DeleteAsync("acme-0", default);

        // A run recreated under the same id must not inherit the deleted run's labels: after the delete the acme
        // entry is gone, so an acme-reach query answers empty from the label sets alone, without reading the hash
        // the recreated globex run now occupies.
        await store.SaveAsync("acme-0", Bytes(), Secured([new("tenant", "globex")]), WorkflowEtag.None, default);

        log.Start();
        (await index.QueryAsync(new WorkflowQuery(Limit: 10, Security: AcmeReach()), default)).Runs.ShouldBeEmpty();
        log.Finish().ShouldNotContain("HGETALL");
    }

    [TestMethod]
    public async Task Changing_a_runs_tags_on_save_moves_its_label_entries()
    {
        // The save script diffs the persisted tag JSON against the incoming one and moves only the difference —
        // the path a §14.2 re-tag takes on this backend. Both directions must hold: the new tenant's entry added
        // (or the run is hidden from its rightful reach, an availability failure) and the old tenant's removed.
        (IWorkflowStateStore store, CommandLog log) = await NewStoreAsync();
        var index = (IWorkflowWaitIndex)store;

        WorkflowEtag first = await store.SaveAsync("retag-0", Bytes(), Secured([new("tenant", "acme")]), WorkflowEtag.None, default);
        await store.SaveAsync("retag-0", Bytes(), Secured([new("tenant", "globex")]), first, default);

        (await index.QueryAsync(new WorkflowQuery(Limit: 10, Security: GlobexReach()), default)).Runs.Count.ShouldBe(1);

        log.Start();
        (await index.QueryAsync(new WorkflowQuery(Limit: 10, Security: AcmeReach()), default)).Runs.ShouldBeEmpty();
        log.Finish().ShouldNotContain("HGETALL");
    }

    [TestMethod]
    public async Task A_tag_key_sharing_a_prefix_cannot_widen_another_tags_reach()
    {
        // The label-set name is the prefix plus the raw key and value; the key's byte length is what keeps that
        // concatenation injective. Without it the tag ("a", "bc") and the tag ("ab", "c") would share a set, and
        // tagging a row ("ab", "c") would put it in the candidates of a rule about ("a", "bc") — invisible in
        // the results (the exact evaluation discards it) but a widening of what the index proposes, so the wire
        // is what the test observes.
        (IWorkflowStateStore store, CommandLog log) = await NewStoreAsync();
        var index = (IWorkflowWaitIndex)store;

        await store.SaveAsync("one", Bytes(), Secured([new("a", "bc")]), WorkflowEtag.None, default);
        await store.SaveAsync("two", Bytes(), Secured([new("ab", "c")]), WorkflowEtag.None, default);

        log.Start();
        WorkflowRunPage page = await index.QueryAsync(new WorkflowQuery(Limit: 10, Security: Reach("a == 'bc'")), default);
        List<string> commands = log.Finish();

        page.Runs.Count.ShouldBe(1);
        page.Runs[0].Id.Value.ShouldBe("one");
        commands.Count(c => c == "HGETALL").ShouldBe(1);
    }

    [TestMethod]
    public async Task A_tag_value_carrying_separator_characters_round_trips()
    {
        // Redis keys are binary-safe, so the label-set name embeds the raw key and value; a value carrying the
        // characters the name itself uses (colons, spaces) must neither be rejected nor land in another set.
        (IWorkflowStateStore store, _) = await NewStoreAsync();
        var index = (IWorkflowWaitIndex)store;

        await store.SaveAsync("odd-0", Bytes(), Secured([new("tenant", "a:b c#d")]), WorkflowEtag.None, default);

        (await index.QueryAsync(new WorkflowQuery(Limit: 10, Security: Reach("tenant == 'a:b c#d'")), default))
            .Runs.Count.ShouldBe(1);
    }

    private static SecurityFilter AcmeReach() => Reach("tenant == 'acme'");

    private static SecurityFilter GlobexReach() => Reach("tenant == 'globex'");

    private static SecurityFilter Reach(string rule)
        => new([SecurityRule.Compile(rule)], new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal));

    private static ReadOnlyMemory<byte> Bytes() => Encoding.UTF8.GetBytes("{}");

    private static WorkflowRunIndexEntry Secured(SecurityTag[] securityTags)
        => new("wf", WorkflowRunStatus.Running, T0, T0, SecurityTags: SecurityTagSet.FromTags(securityTags));

    // Ids are prefixed by tenant so the globex runs a reach test asks for do NOT lead in id order.
    private static async ValueTask SeedAsync(IWorkflowStateStore store, int count)
    {
        for (int i = 0; i < count / 2; ++i)
        {
            await store.SaveAsync($"acme-{i:D3}", Bytes(), Secured([new("tenant", "acme")]), WorkflowEtag.None, default);
            await store.SaveAsync($"globex-{i:D3}", Bytes(), Secured([new("tenant", "globex")]), WorkflowEtag.None, default);
        }
    }

    private static async ValueTask<(IWorkflowStateStore Store, CommandLog Log)> NewStoreAsync()
    {
        string configuration = container.GetConnectionString();

        await using (var admin = await ConnectionMultiplexer.ConnectAsync($"{configuration},allowAdmin=true"))
        {
            await admin.GetServer(admin.GetEndPoints()[0]).FlushDatabaseAsync();
        }

        // The store is opened over a caller-owned connection so the test can register the profiler on it; the
        // connections are disposed with the class.
        var connection = await ConnectionMultiplexer.ConnectAsync(configuration);
        Connections.Add(connection);
        var log = new CommandLog();
        connection.RegisterProfiler(log.CurrentSession);
        return (RedisWorkflowStateStore.Connect(connection), log);
    }

    // Captures the command name of every call issued while a session is active, which is where a query's shape
    // travels on this backend.
    private sealed class CommandLog
    {
        private ProfilingSession? session;

        public ProfilingSession? CurrentSession() => this.session;

        public void Start() => this.session = new ProfilingSession();

        public List<string> Finish()
        {
            List<string> commands = [.. this.session!.FinishProfiling().Select(c => c.Command)];
            this.session = null;
            return commands;
        }
    }
}
