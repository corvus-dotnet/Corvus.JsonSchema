// <copyright file="RedisObservedIdentityReachPushdownTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;
using StackExchange.Redis;
using StackExchange.Redis.Profiling;
using Testcontainers.Redis;

namespace Corvus.Text.Json.Arazzo.Durability.Redis.Tests;

/// <summary>
/// Proves the Redis observed-identity store narrows a reach-scoped search through its security-label sets
/// (design §14.4) — resolved server-side in one Lua evaluation — instead of sweeping the ordering index and
/// discarding documents in process. The observed-identity sibling of <see cref="RedisReachPushdownTests"/>.
/// </summary>
/// <remarks>
/// The command mix discriminates: GET counts how many identity documents were read, ZRANGE marks a sweep of the
/// ordering index, and EVAL/EVALSHA marks the label resolution.
/// </remarks>
[TestClass]
[TestCategory("integration")]
[TestCategory("docker")]
public sealed class RedisObservedIdentityReachPushdownTests
{
    private static readonly ObservedIdentity.GranteeKind Team = ObservedIdentity.GranteeKind.EnumValues.Team;
    private static readonly ObservedIdentity.GranteeKind AllKinds = default;
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
    public async Task A_reach_scoped_search_resolves_the_label_sets_and_reads_only_those_identities()
    {
        (IObservedIdentityStore store, CommandLog log) = await NewStoreAsync();
        for (int i = 0; i < 20; ++i)
        {
            await store.SeenAsync(Team, Str($"acme-{i:D3}"), default, Tags("acme"), true, "test", default);
            await store.SeenAsync(Team, Str($"globex-{i:D3}"), default, Tags("globex"), true, "test", default);
        }

        log.Start();

        // The reachable identities (globex-*) do NOT lead in subject order — acme-* sorts first — so a store
        // that merely swept and took the first matching page could not satisfy the read bound by accident.
        using ObservedIdentityPage page = await store.SearchAsync(ScopeBy("tenant", "globex"), AllKinds, Str(string.Empty), 5, default, default);
        List<string> commands = log.Finish();

        page.Identities.Count.ShouldBe(5);
        page.Identities.ShouldAllBe(i => i.SubjectValueValue.StartsWith("globex-", StringComparison.Ordinal));

        // The label sets were consulted server-side: the reach became one script evaluation...
        commands.ShouldContain(c => c == "EVAL" || c == "EVALSHA");

        // ...the ordering index was never swept...
        commands.ShouldNotContain("ZRANGE");

        // ...and only candidate documents were read. A sweep would have read the 20 acme documents too.
        commands.Count(c => c == "GET").ShouldBeLessThanOrEqualTo(20);
    }

    [TestMethod]
    public async Task An_unreachable_search_reads_no_identities_at_all()
    {
        (IObservedIdentityStore store, CommandLog log) = await NewStoreAsync();
        await store.SeenAsync(Team, Str("acme-team"), default, Tags("acme"), true, "test", default);

        log.Start();
        using ObservedIdentityPage page = await store.SearchAsync(ScopeBy("tenant", "nobody"), AllKinds, Str(string.Empty), 5, default, default);
        List<string> commands = log.Finish();

        page.Identities.Count.ShouldBe(0);

        // An empty candidate set is not "no narrowing": the store must answer without reading a single document.
        commands.ShouldNotContain("GET");
        commands.ShouldNotContain("ZRANGE");
    }

    [TestMethod]
    public async Task An_unrestricted_search_still_sweeps_rather_than_enumerating_labels()
    {
        // The negative control: narrowing must be driven by the reach, not applied always.
        (IObservedIdentityStore store, CommandLog log) = await NewStoreAsync();
        await store.SeenAsync(Team, Str("acme-team"), default, Tags("acme"), true, "test", default);

        log.Start();
        using ObservedIdentityPage page = await store.SearchAsync(AccessContext.System, AllKinds, Str(string.Empty), 5, default, default);
        List<string> commands = log.Finish();

        commands.ShouldContain("ZRANGE");
        commands.ShouldNotContain(c => c == "EVAL" || c == "EVALSHA");
    }

    [TestMethod]
    public async Task A_re_sighting_that_changes_the_tags_moves_the_label_entries()
    {
        // The wire half of the conformance suite's visibility test: after the re-homing, the old tenant's search
        // answers empty from the label sets alone, without reading the document the identity still occupies — a
        // stale entry would produce the same empty page while still costing the read.
        (IObservedIdentityStore store, CommandLog log) = await NewStoreAsync();
        await store.SeenAsync(Team, Str("mobile"), default, Tags("acme"), true, "test", default);
        await store.SeenAsync(Team, Str("mobile"), default, Tags("globex"), true, "test", default);

        using (ObservedIdentityPage globex = await store.SearchAsync(ScopeBy("tenant", "globex"), AllKinds, Str(string.Empty), 5, default, default))
        {
            globex.Identities.Count.ShouldBe(1);
        }

        log.Start();
        using (ObservedIdentityPage acme = await store.SearchAsync(ScopeBy("tenant", "acme"), AllKinds, Str(string.Empty), 5, default, default))
        {
            acme.Identities.Count.ShouldBe(0);
        }

        log.Finish().ShouldNotContain("GET");
    }

    [TestMethod]
    public async Task A_tag_key_sharing_a_prefix_cannot_widen_another_tags_reach()
    {
        // The label-set name's byte-length prefix is what keeps the raw concatenation injective — an index
        // widening is invisible in the results (the exact evaluation discards it), so the wire is what the test
        // observes.
        (IObservedIdentityStore store, CommandLog log) = await NewStoreAsync();
        await store.SeenAsync(Team, Str("one"), default, SecurityTagSet.FromTags([new SecurityTag("a", "bc")]), true, "test", default);
        await store.SeenAsync(Team, Str("two"), default, SecurityTagSet.FromTags([new SecurityTag("ab", "c")]), true, "test", default);

        log.Start();
        using ObservedIdentityPage page = await store.SearchAsync(ScopeBy("a", "bc"), AllKinds, Str(string.Empty), 5, default, default);
        List<string> commands = log.Finish();

        page.Identities.Single().SubjectValueValue.ShouldBe("one");
        commands.Count(c => c == "GET").ShouldBe(1);
    }

    [TestMethod]
    public async Task A_tag_value_carrying_separator_characters_round_trips()
    {
        (IObservedIdentityStore store, _) = await NewStoreAsync();
        await store.SeenAsync(Team, Str("odd"), default, SecurityTagSet.FromTags([new SecurityTag("tenant", "a:b c#d")]), true, "test", default);

        using ObservedIdentityPage page = await store.SearchAsync(ScopeBy("tenant", "a:b c#d"), AllKinds, Str(string.Empty), 5, default, default);
        page.Identities.Count.ShouldBe(1);
    }

    private static SecurityTagSet Tags(string tenant) => SecurityTagSet.FromTags([new SecurityTag("tenant", tenant)]);

    private static JsonString Str(string value) => JsonString.ParseValue($"\"{value}\"");

    private static AccessContext ScopeBy(string key, string value) => AccessContext.Uniform(
        new SecurityFilter([SecurityRule.Compile($"{key} == $claim.{key}")], new Dictionary<string, IReadOnlyList<string>> { [key] = [value] }));

    private static async ValueTask<(IObservedIdentityStore Store, CommandLog Log)> NewStoreAsync()
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
        return (RedisObservedIdentityStore.Connect(connection), log);
    }

    // Records the command name of every call issued while a session is active, which is where a query's shape
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
