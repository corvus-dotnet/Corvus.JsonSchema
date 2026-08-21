// <copyright file="MongoReachPushdownTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Concurrent;
using System.Text;
using Corvus.Text.Json.Arazzo.Durability;
using global::MongoDB.Bson;
using global::MongoDB.Driver;
using global::MongoDB.Driver.Core.Events;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;
using Testcontainers.MongoDb;

namespace Corvus.Text.Json.Arazzo.Durability.Mongo.Tests;

/// <summary>
/// Proves the Mongo stores push the row-security reach (§14.2) down to the server as an indexed predicate
/// (design §14.4) rather than streaming the collection and discarding rows in process.
/// </summary>
/// <remarks>
/// Correctness under a reach filter does not discriminate here — the in-process filter was correct too, which is
/// exactly why the shared conformance oracle passed while the work was unbounded. These tests therefore observe
/// the <b>wire</b>: the command the driver actually sends must carry the reach predicate, and must stay bounded
/// by the page limit or the count cap instead of asking for an open cursor over every tenant's rows.
/// </remarks>
[TestClass]
[TestCategory("integration")]
[TestCategory("docker")]
public sealed class MongoReachPushdownTests
{
    private const string DatabaseName = "arazzo";
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    // The event's Command is a RawBsonDocument over a buffer the driver disposes once the command completes, so the
    // subscriber has to take its own copy while the event is being raised rather than at assertion time.
    private static readonly ConcurrentQueue<(string Name, string Json)> Commands = new();
    private static MongoDbContainer container = null!;
    private static IMongoClient client = null!;

    [ClassInitialize]
    public static async Task ClassInitAsync(TestContext context)
    {
        container = new MongoDbBuilder().WithImage("mongo:7").Build();
        await container.StartAsync();

        // Subscribe to the driver's command-started events: the assertions below are about what the store asks the
        // server to do, which is the only thing that separates a pushed-down filter from a correct in-process one.
        MongoClientSettings settings = MongoClientSettings.FromConnectionString(container.GetConnectionString());
        settings.ClusterConfigurator = builder => builder.Subscribe<CommandStartedEvent>(e => Commands.Enqueue((e.CommandName, e.Command.ToJson())));
        client = new MongoClient(settings);
    }

    [ClassCleanup]
    public static async Task ClassCleanupAsync()
    {
        if (container is not null)
        {
            await container.DisposeAsync();
        }
    }

    [TestMethod]
    public async Task A_reach_filtered_run_query_sends_the_reach_predicate_and_keeps_the_page_limit()
    {
        IWorkflowStateStore store = await NewStateStoreAsync();
        await SeedRunsAsync(store, count: 40);

        Commands.Clear();
        WorkflowRunPage page = await ((IWorkflowWaitIndex)store).QueryAsync(
            new WorkflowQuery(Limit: 5, Security: AcmeReach()), default);

        page.Runs.Count.ShouldBe(5);

        BsonDocument find = SingleCommand("find");

        // The reach reached the server: the run tags appear in the query the server planned, not in a client loop.
        find["filter"].ToJson().ShouldContain("securityTags");

        // And the work stayed bounded: a page of five asks for six rows, never an open cursor over the collection.
        find.Contains("limit").ShouldBeTrue("a reach-filtered page must still bound the server-side scan");
        find["limit"].ToInt64().ShouldBe(6);
    }

    [TestMethod]
    public async Task A_reach_filtered_run_count_uses_the_servers_bounded_count_rather_than_a_cursor()
    {
        IWorkflowStateStore store = await NewStateStoreAsync();
        await SeedRunsAsync(store, count: 40);

        Commands.Clear();
        (int Count, bool Capped) counted = await ((IWorkflowWaitIndex)store).CountAsync(
            new WorkflowQuery(Security: AcmeReach()), 100, default);

        counted.ShouldBe((20, false));

        // A native bounded count is an aggregate ($match/$limit/$group), never a find whose rows the client tallies.
        Commands.Any(c => c.Name == "find").ShouldBeFalse("a reach-filtered count must not stream rows to the client");
        SingleCommand("aggregate")["pipeline"].ToJson().ShouldContain("securityTags");
    }

    [TestMethod]
    public async Task A_reach_filtered_distinct_catalog_query_collapses_representatives_on_the_server()
    {
        IWorkflowCatalogStore store = await NewCatalogStoreAsync();
        await SeedCatalogAsync(store, count: 20);

        Commands.Clear();
        using CatalogPage page = await store.QueryAsync(
            new CatalogQuery(DistinctWorkflows: true, Limit: 5, Security: AcmeReach()), default);

        page.Versions.Count.ShouldBe(5);

        BsonDocument pipeline = SingleCommand("aggregate");
        string stages = pipeline["pipeline"].ToJson();

        // The reach is a pipeline stage, so the winning version per base can be chosen server-side: the $group
        // collapse and the $limit are what the in-process representative walk had to give up.
        stages.ShouldContain("securityTags");
        stages.ShouldContain("$group");
        stages.ShouldContain("$limit");
    }

    private static SecurityFilter AcmeReach()
        => new(
            [SecurityRule.Compile("tenant == 'acme'")],
            new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal));

    // The one command of this name the store issued. More than one means a retry or a second round trip, which is
    // itself worth failing on: the assertions below are about a single planned query.
    private static BsonDocument SingleCommand(string name)
    {
        List<string> matches = [.. Commands.Where(c => c.Name == name).Select(c => c.Json)];
        matches.Count.ShouldBe(1, $"expected exactly one '{name}' command, saw {matches.Count}");
        return BsonDocument.Parse(matches[0]);
    }

    private static async ValueTask<IWorkflowStateStore> NewStateStoreAsync()
    {
        await client.DropDatabaseAsync(DatabaseName);
        await MongoWorkflowStateStore.PrepareAsync(client, DatabaseName);
        return await MongoWorkflowStateStore.ConnectAsync(client, DatabaseName);
    }

    private static async ValueTask<IWorkflowCatalogStore> NewCatalogStoreAsync()
    {
        await client.DropDatabaseAsync(DatabaseName);
        await MongoWorkflowCatalogStore.PrepareAsync(client, DatabaseName);
        return await MongoWorkflowCatalogStore.ConnectAsync(client, DatabaseName);
    }

    // The run's full (environment, runId) address (ADR 0065 decision 9).
    private static WorkflowRunAddress A(string runId) => new("development", new WorkflowRunId(runId));

    // Half the runs are the reaching tenant's and half another's, interleaved, so a page of five is satisfied early
    // in the _id order and an unbounded scan is a choice rather than a necessity.
    private static async ValueTask SeedRunsAsync(IWorkflowStateStore store, int count)
    {
        for (int i = 0; i < count; ++i)
        {
            SecurityTag[] tags = [new("tenant", i % 2 == 0 ? "acme" : "globex")];
            var entry = new WorkflowRunIndexEntry("wf", WorkflowRunStatus.Running, T0, T0, SecurityTags: SecurityTagSet.FromTags(tags));
            await store.SaveAsync(A($"run-{i:D3}"), Encoding.UTF8.GetBytes("{}"), entry, WorkflowEtag.None, default);
        }
    }

    private static async ValueTask SeedCatalogAsync(IWorkflowCatalogStore store, int count)
    {
        for (int i = 0; i < count; ++i)
        {
            SecurityTag[] tags = [new("tenant", i % 2 == 0 ? "acme" : "globex")];
            var metadata = new CatalogMetadata(
                new CatalogOwner("Team A", "team-a@example.com"),
                "alice",
                default,
                SecurityTagSet.FromTags(tags));
            (await store.AddAsync($"flow-{i:D3}", Package($"flow-{i:D3}"), metadata, default)).Dispose();
        }
    }

    private static ReadOnlyMemory<byte> Package(string workflowId)
    {
        byte[] workflow = Encoding.UTF8.GetBytes($$"""
        {
          "arazzo": "1.1.0",
          "info": { "title": "Nightly Reconcile", "description": "Reconciles state nightly." },
          "sourceDescriptions": [ { "name": "petstore", "url": "./petstore.json", "type": "openapi" } ],
          "workflows": [ { "workflowId": "{{workflowId}}", "steps": [] } ]
        }
        """);
        byte[] petstore = Encoding.UTF8.GetBytes("""{"openapi":"3.1.0","info":{"title":"Petstore","version":"1.0.0"}}""");
        return CatalogPackage.Build(workflow, [new KeyValuePair<string, byte[]>("petstore", petstore)]);
    }
}