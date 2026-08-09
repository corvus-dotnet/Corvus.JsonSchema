// <copyright file="RedisCatalogReachPushdownTests.cs" company="Endjin Limited">
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
/// Proves the Redis catalog store narrows a reach-filtered query through its security-label sets (design §14.4)
/// — resolved server-side in one Lua evaluation — instead of sweeping the ordering index and discarding docs in
/// process. The catalog sibling of <see cref="RedisReachPushdownTests"/>.
/// </summary>
/// <remarks>
/// The command mix discriminates: HGET counts how many version docs were read, ZRANGE marks a sweep of the
/// ordering index, and EVAL/EVALSHA marks the label resolution.
/// </remarks>
[TestClass]
[TestCategory("integration")]
[TestCategory("docker")]
public sealed class RedisCatalogReachPushdownTests
{
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
    public async Task A_reach_filtered_query_resolves_the_label_sets_and_reads_only_those_versions()
    {
        (IWorkflowCatalogStore store, CommandLog log) = await NewStoreAsync();
        await SeedAsync(store, count: 40);

        log.Start();

        // The reachable versions (globex-*) do NOT lead in sort-key order — acme-* sorts first — so a store
        // that merely swept and took the first matching page would read the acme docs on the way and the HGET
        // bound below could not hold by accident.
        using CatalogPage page = await store.QueryAsync(new CatalogQuery(Limit: 5, Security: GlobexReach()), default);
        List<string> commands = log.Finish();

        page.Versions.Count.ShouldBe(5);
        page.Versions.ShouldAllBe(v => v.Ref.BaseWorkflowId.StartsWith("globex-", StringComparison.Ordinal));

        // The label sets were consulted server-side: the reach became one script evaluation...
        commands.ShouldContain(c => c == "EVAL" || c == "EVALSHA");

        // ...the ordering index was never swept...
        commands.ShouldNotContain("ZRANGE");

        // ...and only candidate docs were read. A sweep would have read the 20 acme docs too.
        commands.Count(c => c == "HGET").ShouldBeLessThanOrEqualTo(20);
    }

    [TestMethod]
    public async Task An_unreachable_query_reads_no_versions_at_all()
    {
        (IWorkflowCatalogStore store, CommandLog log) = await NewStoreAsync();
        await SeedAsync(store, count: 8);

        log.Start();
        using CatalogPage page = await store.QueryAsync(new CatalogQuery(Limit: 5, Security: Reach("tenant == 'nobody'")), default);
        List<string> commands = log.Finish();

        page.Versions.ShouldBeEmpty();

        // An empty candidate set is not "no narrowing": the store must answer without reading a single doc.
        commands.ShouldNotContain("HGET");
        commands.ShouldNotContain("ZRANGE");
    }

    [TestMethod]
    public async Task An_unfiltered_query_still_sweeps_rather_than_enumerating_label_sets()
    {
        // The negative control for the first test: narrowing must be driven by the reach, not applied always.
        (IWorkflowCatalogStore store, CommandLog log) = await NewStoreAsync();
        await SeedAsync(store, count: 8);

        log.Start();
        using CatalogPage page = await store.QueryAsync(new CatalogQuery(Limit: 5), default);
        List<string> commands = log.Finish();

        commands.ShouldContain("ZRANGE");
        commands.ShouldNotContain(c => c == "EVAL" || c == "EVALSHA");
    }

    [TestMethod]
    public async Task Deleting_a_version_removes_its_label_entries()
    {
        (IWorkflowCatalogStore store, CommandLog log) = await NewStoreAsync();

        (await store.AddAsync("secure-flow", Package("secure-flow"), Meta("acme"), default)).Dispose();
        using (CatalogPage page = await store.QueryAsync(new CatalogQuery(Limit: 10, Security: AcmeReach()), default))
        {
            page.Versions.Count.ShouldBe(1);
        }

        (await store.DeleteAsync("secure-flow", 1, default)).ShouldBeTrue();

        // The re-add takes version 2 (the counter never reuses a number), but the acme entry for version 1 must
        // be gone all the same: an acme-reach query answers empty from the label sets alone, without reading any
        // doc — a stale entry would produce the same empty page while still costing the read, so the wire is
        // what the test observes.
        (await store.AddAsync("secure-flow", Package("secure-flow"), Meta("globex"), default)).Dispose();
        log.Start();
        using (CatalogPage page = await store.QueryAsync(new CatalogQuery(Limit: 10, Security: AcmeReach()), default))
        {
            page.Versions.ShouldBeEmpty();
        }

        log.Finish().ShouldNotContain("HGET");
    }

    [TestMethod]
    public async Task Retagging_a_version_moves_its_label_entries()
    {
        // A §14.2 re-tag replaces the version's security tags in place, so the label diff must be maintained —
        // the new tenant's entry added (or the version is hidden from its rightful reach, an availability
        // failure) and the old tenant's removed.
        (IWorkflowCatalogStore store, CommandLog log) = await NewStoreAsync();

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

        log.Start();
        using (CatalogPage page = await store.QueryAsync(new CatalogQuery(Limit: 10, Security: AcmeReach()), default))
        {
            page.Versions.ShouldBeEmpty();
        }

        log.Finish().ShouldNotContain("HGET");
    }

    [TestMethod]
    public async Task A_tag_key_sharing_a_prefix_cannot_widen_another_tags_reach()
    {
        // The label-set name embeds the key's byte length, which is what keeps the raw concatenation injective:
        // without it ("a", "bc") and ("ab", "c") would share a set, an index widening invisible in the results
        // (the exact evaluation discards it), so the wire is what the test observes.
        (IWorkflowCatalogStore store, CommandLog log) = await NewStoreAsync();

        (await store.AddAsync("one", Package("one"), MetaTags([new("a", "bc")]), default)).Dispose();
        (await store.AddAsync("two", Package("two"), MetaTags([new("ab", "c")]), default)).Dispose();

        log.Start();
        using CatalogPage page = await store.QueryAsync(new CatalogQuery(Limit: 10, Security: Reach("a == 'bc'")), default);
        List<string> commands = log.Finish();

        page.Versions.Count.ShouldBe(1);
        page.Versions[0].Ref.BaseWorkflowId.ShouldBe("one");
        commands.Count(c => c == "HGET").ShouldBe(1);
    }

    [TestMethod]
    public async Task A_tag_value_carrying_separator_characters_round_trips()
    {
        // Redis keys are binary-safe, so the label-set name embeds the raw key and value; a value carrying the
        // characters the name itself uses (colons, spaces) must neither be rejected nor land in another set.
        (IWorkflowCatalogStore store, _) = await NewStoreAsync();

        (await store.AddAsync("odd-flow", Package("odd-flow"), MetaTags([new("tenant", "a:b c#d")]), default)).Dispose();

        using CatalogPage page = await store.QueryAsync(new CatalogQuery(Limit: 10, Security: Reach("tenant == 'a:b c#d'")), default);
        page.Versions.Count.ShouldBe(1);
    }

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

    // Bases are prefixed by tenant so the globex versions a reach test asks for do NOT lead in sort-key order.
    private static async ValueTask SeedAsync(IWorkflowCatalogStore store, int count)
    {
        for (int i = 0; i < count / 2; ++i)
        {
            (await store.AddAsync($"acme-{i:D3}", Package($"acme-{i:D3}"), Meta("acme"), default)).Dispose();
            (await store.AddAsync($"globex-{i:D3}", Package($"globex-{i:D3}"), Meta("globex"), default)).Dispose();
        }
    }

    private static async ValueTask<(IWorkflowCatalogStore Store, CommandLog Log)> NewStoreAsync()
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
        return (RedisWorkflowCatalogStore.Connect(connection), log);
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
