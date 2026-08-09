// <copyright file="MongoManagementReachPushdownTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Concurrent;
using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Environments;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Corvus.Text.Json.Arazzo.Durability.Sources;
using Corvus.Text.Json.Arazzo.Durability.WorkspaceWorkflows;
using global::MongoDB.Bson;
using global::MongoDB.Driver;
using global::MongoDB.Driver.Core.Events;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;
using Testcontainers.MongoDb;
using Environment = Corvus.Text.Json.Arazzo.Durability.Environments.Environment;

namespace Corvus.Text.Json.Arazzo.Durability.Mongo.Tests;

/// <summary>
/// Proves the four Mongo management stores (environment, source, source-credential, workspace-workflow) push the
/// §14.2 reach down to the server as an indexed predicate over the <c>securityTags</c> mirror rather than streaming
/// the collection and discarding rows in process, and that a re-tagging update re-sets the mirror in the same write.
/// The assertions observe the wire — the commands the driver actually sends — because correctness alone does not
/// discriminate a pushed-down filter from a correct in-process one.
/// </summary>
[TestClass]
[TestCategory("integration")]
[TestCategory("docker")]
public sealed class MongoManagementReachPushdownTests
{
    private const string DatabaseName = "arazzo";

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
    public async Task A_reach_filtered_environment_list_sends_the_reach_predicate_and_keeps_the_page_limit()
    {
        MongoEnvironmentStore store = await NewEnvironmentStoreAsync();
        await SeedEnvironmentAsync(store, "production", "acme");
        await SeedEnvironmentAsync(store, "staging", "globex");

        Commands.Clear();
        using (EnvironmentPage page = await store.ListAsync(Scope("acme"), 5, default, default))
        {
            page.Environments.Select(e => e.ManagementTagsValue.ToList().Single().Value).ShouldBe(["acme"]);
        }

        BsonDocument find = SingleCommand("find");
        find["filter"].ToJson().ShouldContain("securityTags");
        find.Contains("limit").ShouldBeTrue("a reach-filtered page must still bound the server-side scan");
        find["limit"].ToInt64().ShouldBe(6);
    }

    [TestMethod]
    public async Task An_unrestricted_environment_list_carries_no_reach_predicate()
    {
        MongoEnvironmentStore store = await NewEnvironmentStoreAsync();
        await SeedEnvironmentAsync(store, "production", "acme");

        Commands.Clear();
        using (EnvironmentPage page = await store.ListAsync(AccessContext.System, 5, default, default))
        {
            page.Environments.Count.ShouldBe(1);
        }

        SingleCommand("find")["filter"].ToJson().ShouldNotContain("securityTags");
    }

    [TestMethod]
    public async Task A_reach_filtered_environment_count_uses_the_servers_bounded_count()
    {
        MongoEnvironmentStore store = await NewEnvironmentStoreAsync();
        await SeedEnvironmentAsync(store, "production", "acme");
        await SeedEnvironmentAsync(store, "staging", "globex");

        Commands.Clear();
        (await store.CountAsync(Scope("acme"), 100, default)).ShouldBe((1, false));

        Commands.Any(c => c.Name == "find").ShouldBeFalse("a reach-filtered count must not stream rows to the client");
        SingleCommand("aggregate")["pipeline"].ToJson().ShouldContain("securityTags");
    }

    [TestMethod]
    public async Task A_re_tagging_environment_update_re_sets_the_mirror_in_the_same_write()
    {
        MongoEnvironmentStore store = await NewEnvironmentStoreAsync();
        await SeedEnvironmentAsync(store, "production", "acme");

        Commands.Clear();
        using (ParsedJsonDocument<Environment> reTag = Environment.Draft("production", null, null, Tenant("globex")))
        using (ParsedJsonDocument<Environment>? updated = await store.UpdateAsync("production", reTag.RootElement, WorkflowEtag.None, "carol", AccessContext.System, default))
        {
            updated.ShouldNotBeNull();
        }

        string update = SingleCommand("update").ToJson();
        update.ShouldContain("securityTags");
        update.ShouldContain("globex");
    }

    [TestMethod]
    public async Task A_reach_filtered_source_list_sends_the_reach_predicate_and_keeps_the_page_limit()
    {
        MongoSourceStore store = await NewSourceStoreAsync();
        await SeedSourceAsync(store, "petstore", "acme");
        await SeedSourceAsync(store, "ledger", "globex");

        Commands.Clear();
        using (SourcePage page = await store.ListAsync(Scope("acme"), 5, default, default))
        {
            page.Sources.Select(s => s.ManagementTagsValue.ToList().Single().Value).ShouldBe(["acme"]);
        }

        BsonDocument find = SingleCommand("find");
        find["filter"].ToJson().ShouldContain("securityTags");
        find["limit"].ToInt64().ShouldBe(6);
    }

    [TestMethod]
    public async Task A_reach_filtered_source_count_uses_the_servers_bounded_count()
    {
        MongoSourceStore store = await NewSourceStoreAsync();
        await SeedSourceAsync(store, "petstore", "acme");
        await SeedSourceAsync(store, "ledger", "globex");

        Commands.Clear();
        (await store.CountAsync(Scope("acme"), 100, default)).ShouldBe((1, false));

        Commands.Any(c => c.Name == "find").ShouldBeFalse("a reach-filtered count must not stream rows to the client");
        SingleCommand("aggregate")["pipeline"].ToJson().ShouldContain("securityTags");
    }

    [TestMethod]
    public async Task A_reach_filtered_credential_list_sends_the_reach_predicate_and_keeps_the_page_limit()
    {
        MongoSourceCredentialStore store = await NewCredentialStoreAsync();
        await SeedCredentialAsync(store, "petstore", "production", "acme");
        await SeedCredentialAsync(store, "ledger", "production", "globex");

        Commands.Clear();
        using (SourceCredentialPage page = await store.ListAsync(Scope("acme"), 5, default, default))
        {
            page.Bindings.Select(b => b.ManagementTagsValue.ToList().Single().Value).ShouldBe(["acme"]);
        }

        BsonDocument find = SingleCommand("find");
        find["filter"].ToJson().ShouldContain("securityTags");
        find["limit"].ToInt64().ShouldBe(6);
    }

    [TestMethod]
    public async Task A_reach_filtered_credential_count_uses_the_servers_bounded_count()
    {
        MongoSourceCredentialStore store = await NewCredentialStoreAsync();
        await SeedCredentialAsync(store, "petstore", "production", "acme");
        await SeedCredentialAsync(store, "ledger", "production", "globex");

        Commands.Clear();
        (await store.CountAsync(Scope("acme"), 100, default)).ShouldBe((1, false));

        Commands.Any(c => c.Name == "find").ShouldBeFalse("a reach-filtered count must not stream rows to the client");
        SingleCommand("aggregate")["pipeline"].ToJson().ShouldContain("securityTags");
    }

    [TestMethod]
    public async Task A_reach_filtered_working_copy_list_sends_the_reach_predicate_and_keeps_the_page_limit()
    {
        MongoWorkspaceWorkflowStore store = await NewWorkspaceStoreAsync();
        await SeedWorkingCopyAsync(store, "acme");
        await SeedWorkingCopyAsync(store, "globex");

        Commands.Clear();
        using (WorkspaceWorkflowPage page = await store.ListAsync(Scope("acme"), 5, default, default))
        {
            page.WorkingCopies.Select(w => w.ManagementTagsValue.ToList().Single().Value).ShouldBe(["acme"]);
        }

        BsonDocument find = SingleCommand("find");
        find["filter"].ToJson().ShouldContain("securityTags");
        find["limit"].ToInt64().ShouldBe(6);
    }

    [TestMethod]
    public async Task A_reach_filtered_working_copy_count_uses_the_servers_bounded_count()
    {
        MongoWorkspaceWorkflowStore store = await NewWorkspaceStoreAsync();
        await SeedWorkingCopyAsync(store, "acme");
        await SeedWorkingCopyAsync(store, "globex");

        Commands.Clear();
        (await store.CountAsync(Scope("acme"), 100, default)).ShouldBe((1, false));

        Commands.Any(c => c.Name == "find").ShouldBeFalse("a reach-filtered count must not stream rows to the client");
        SingleCommand("aggregate")["pipeline"].ToJson().ShouldContain("securityTags");
    }

    private static async ValueTask<MongoEnvironmentStore> NewEnvironmentStoreAsync()
    {
        await client.DropDatabaseAsync(DatabaseName);
        await MongoEnvironmentStore.PrepareAsync(client, DatabaseName);
        return await MongoEnvironmentStore.ConnectAsync(client, DatabaseName);
    }

    private static async ValueTask<MongoSourceStore> NewSourceStoreAsync()
    {
        await client.DropDatabaseAsync(DatabaseName);
        await MongoSourceStore.PrepareAsync(client, DatabaseName);
        return await MongoSourceStore.ConnectAsync(client, DatabaseName);
    }

    private static async ValueTask<MongoSourceCredentialStore> NewCredentialStoreAsync()
    {
        await client.DropDatabaseAsync(DatabaseName);
        await MongoSourceCredentialStore.PrepareAsync(client, DatabaseName);
        return await MongoSourceCredentialStore.ConnectAsync(client, DatabaseName);
    }

    private static async ValueTask<MongoWorkspaceWorkflowStore> NewWorkspaceStoreAsync()
    {
        await client.DropDatabaseAsync(DatabaseName);
        await MongoWorkspaceWorkflowStore.PrepareAsync(client, DatabaseName);
        return await MongoWorkspaceWorkflowStore.ConnectAsync(client, DatabaseName);
    }

    private static async ValueTask SeedEnvironmentAsync(MongoEnvironmentStore store, string name, string tenant)
    {
        using ParsedJsonDocument<Environment> draft = Environment.Draft(name, null, null, Tenant(tenant));
        using ParsedJsonDocument<Environment> added = await store.AddAsync(draft.RootElement, "system", default);
    }

    private static async ValueTask SeedSourceAsync(MongoSourceStore store, string name, string tenant)
    {
        using ParsedJsonDocument<RegisteredSource> draft = RegisteredSource.Draft(name, "openapi", DocUtf8("v1"), null, null, Tenant(tenant));
        using ParsedJsonDocument<RegisteredSource> added = await store.AddAsync(draft.RootElement, "system", default);
    }

    private static async ValueTask SeedCredentialAsync(MongoSourceCredentialStore store, string sourceName, string environment, string tenant)
    {
        var definition = new SourceCredentialDefinition(
            sourceName,
            environment,
            SourceCredentialKind.ApiKey,
            [new SecretReferenceDefinition("value", $"keyvault://{sourceName}-{environment}-{tenant}-apikey")],
            ManagementTags: Tenant(tenant),
            UsageTags: Tenant(tenant));
        using ParsedJsonDocument<SourceCredentialBinding> added = await store.AddAsync(definition, "system", default);
    }

    private static async ValueTask SeedWorkingCopyAsync(MongoWorkspaceWorkflowStore store, string tenant)
    {
        using ParsedJsonDocument<WorkspaceWorkflow> draft = WorkspaceWorkflow.Draft("wc", DocUtf8("v1"), default, null, null, Tenant(tenant));
        using ParsedJsonDocument<WorkspaceWorkflow> added = await store.AddAsync(draft.RootElement, "system", default);
    }

    private static ReadOnlyMemory<byte> DocUtf8(string marker)
        => Encoding.UTF8.GetBytes($$"""{"arazzo":"1.1.0","x-marker":"{{marker}}"}""");

    private static SecurityTagSet Tenant(string tenant) => SecurityTagSet.FromTags([new SecurityTag("tenant", tenant)]);

    // A read/write/purge reach that admits exactly the rows tagged tenant=<tenant> (tenant == $claim.tenant resolved
    // against a single-tenant claim).
    private static AccessContext Scope(string tenant) => AccessContext.Uniform(
        new SecurityFilter([SecurityRule.Compile("tenant == $claim.tenant")], new Dictionary<string, IReadOnlyList<string>> { ["tenant"] = [tenant] }));

    // The one command of this name the store issued. More than one means a retry or a second round trip, which is
    // itself worth failing on: the assertions are about a single planned operation.
    private static BsonDocument SingleCommand(string name)
    {
        List<string> matches = [.. Commands.Where(c => c.Name == name).Select(c => c.Json)];
        matches.Count.ShouldBe(1, $"expected exactly one '{name}' command, saw {matches.Count}");
        return BsonDocument.Parse(matches[0]);
    }
}