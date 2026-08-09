// <copyright file="CosmosManagementReachPushdownTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Net;
using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability.Environments;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Corvus.Text.Json.Arazzo.Durability.Sources;
using Corvus.Text.Json.Arazzo.Durability.WorkspaceWorkflows;
using Microsoft.Azure.Cosmos;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;
using Testcontainers.CosmosDb;
using Environment = Corvus.Text.Json.Arazzo.Durability.Environments.Environment;

namespace Corvus.Text.Json.Arazzo.Durability.Cosmos.Tests;

/// <summary>
/// Wire-observing proof that the four Cosmos management stores (environment, source, source-credential,
/// workspace-workflow) push the §14.2 read reach into their queries: a scoped caller's list/count SQL carries the
/// <c>securityTags</c> <c>EXISTS</c> predicate, the unrestricted system caller's carries none, and the reach count is
/// bounded server-side (<c>OFFSET 0 LIMIT</c>). A capture handler in the client pipeline records every request body
/// the SDK sends, so the assertions observe the store's actual traffic rather than trusting its code shape.
/// </summary>
[TestClass]
[TestCategory("integration")]
[TestCategory("docker")]
[TestCategory("cosmos")]
public sealed class CosmosManagementReachPushdownTests
{
    private const string DatabaseName = "arazzo";
    private static CosmosDbContainer container = null!;
    private static CosmosClient client = null!;
    private static QueryLog log = null!;

    [ClassInitialize]
    public static async Task ClassInitAsync(TestContext context)
    {
        container = new CosmosDbBuilder().Build();
        await container.StartAsync();

        log = new QueryLog();
        CosmosClientOptions options = CosmosEnvironmentStore.CreateClientOptions();
        options.ConnectionMode = ConnectionMode.Gateway;
        options.HttpClientFactory = () => container.HttpClient;
        options.LimitToEndpoint = true;
        options.CustomHandlers.Add(log);
        client = new CosmosClient(container.GetConnectionString(), options);

        // The emulator's gateway reports ready before its query engine finishes starting ("pgcosmos extension is
        // still starting" 503s), so probe until a control-plane round trip succeeds rather than letting the first
        // test absorb the failures.
        for (int attempt = 0; ; attempt++)
        {
            try
            {
                await client.CreateDatabaseIfNotExistsAsync(DatabaseName);
                break;
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.ServiceUnavailable && attempt < 120)
            {
                await Task.Delay(TimeSpan.FromSeconds(1));
            }
        }
    }

    [ClassCleanup]
    public static async Task ClassCleanupAsync()
    {
        client?.Dispose();
        if (container is not null)
        {
            await container.DisposeAsync();
        }
    }

    [TestMethod]
    public async Task A_reach_filtered_environment_list_sends_the_reach_predicate_to_cosmos()
    {
        CosmosEnvironmentStore store = await NewEnvironmentStoreAsync();
        await SeedEnvironmentAsync(store, "production", "acme");
        await SeedEnvironmentAsync(store, "staging", "globex");

        log.Clear();
        using (EnvironmentPage page = await store.ListAsync(Scope("acme"), 10, default, default))
        {
            page.Environments.Select(e => e.ManagementTagsValue.ToList().Single().Value).ShouldBe(["acme"]);
        }

        List<string> queries = [.. log.Queries.Where(q => q.Contains("ORDER BY c.name", StringComparison.Ordinal))];
        queries.ShouldNotBeEmpty();
        queries.ShouldAllBe(q => q.Contains("securityTags", StringComparison.Ordinal) && q.Contains("EXISTS", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task An_unrestricted_environment_list_carries_no_reach_predicate()
    {
        CosmosEnvironmentStore store = await NewEnvironmentStoreAsync();
        await SeedEnvironmentAsync(store, "production", "acme");

        log.Clear();
        using (EnvironmentPage page = await store.ListAsync(AccessContext.System, 10, default, default))
        {
            page.Environments.Count.ShouldBe(1);
        }

        List<string> queries = [.. log.Queries.Where(q => q.Contains("ORDER BY c.name", StringComparison.Ordinal))];
        queries.ShouldNotBeEmpty();
        queries.ShouldAllBe(q => !q.Contains("securityTags", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task A_reach_filtered_environment_count_is_bounded_server_side()
    {
        CosmosEnvironmentStore store = await NewEnvironmentStoreAsync();
        await SeedEnvironmentAsync(store, "production", "acme");
        await SeedEnvironmentAsync(store, "staging", "globex");

        log.Clear();
        (await store.CountAsync(Scope("acme"), 5, default)).Count.ShouldBe(1);

        List<string> queries = [.. log.Queries.Where(q => q.Contains("OFFSET 0 LIMIT", StringComparison.Ordinal))];
        queries.ShouldNotBeEmpty();
        queries.ShouldAllBe(q => q.Contains("securityTags", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task A_reach_filtered_source_list_sends_the_reach_predicate_to_cosmos()
    {
        CosmosSourceStore store = await NewSourceStoreAsync();
        await SeedSourceAsync(store, "petstore", "acme");
        await SeedSourceAsync(store, "ledger", "globex");

        log.Clear();
        using (SourcePage page = await store.ListAsync(Scope("acme"), 10, default, default))
        {
            page.Sources.Select(s => s.ManagementTagsValue.ToList().Single().Value).ShouldBe(["acme"]);
        }

        List<string> queries = [.. log.Queries.Where(q => q.Contains("ORDER BY c.name", StringComparison.Ordinal))];
        queries.ShouldNotBeEmpty();
        queries.ShouldAllBe(q => q.Contains("securityTags", StringComparison.Ordinal) && q.Contains("EXISTS", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task An_unrestricted_source_list_carries_no_reach_predicate()
    {
        CosmosSourceStore store = await NewSourceStoreAsync();
        await SeedSourceAsync(store, "petstore", "acme");

        log.Clear();
        using (SourcePage page = await store.ListAsync(AccessContext.System, 10, default, default))
        {
            page.Sources.Count.ShouldBe(1);
        }

        List<string> queries = [.. log.Queries.Where(q => q.Contains("ORDER BY c.name", StringComparison.Ordinal))];
        queries.ShouldNotBeEmpty();
        queries.ShouldAllBe(q => !q.Contains("securityTags", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task A_reach_filtered_source_count_is_bounded_server_side()
    {
        CosmosSourceStore store = await NewSourceStoreAsync();
        await SeedSourceAsync(store, "petstore", "acme");
        await SeedSourceAsync(store, "ledger", "globex");

        log.Clear();
        (await store.CountAsync(Scope("acme"), 5, default)).Count.ShouldBe(1);

        List<string> queries = [.. log.Queries.Where(q => q.Contains("OFFSET 0 LIMIT", StringComparison.Ordinal))];
        queries.ShouldNotBeEmpty();
        queries.ShouldAllBe(q => q.Contains("securityTags", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task A_reach_filtered_credential_list_sends_the_reach_predicate_to_cosmos()
    {
        CosmosSourceCredentialStore store = await NewCredentialStoreAsync();
        await SeedCredentialAsync(store, "petstore", "production", "acme");
        await SeedCredentialAsync(store, "ledger", "production", "globex");

        log.Clear();
        using (SourceCredentialPage page = await store.ListAsync(Scope("acme"), 10, default, default))
        {
            page.Bindings.Select(b => b.ManagementTagsValue.ToList().Single().Value).ShouldBe(["acme"]);
        }

        List<string> queries = [.. log.Queries.Where(q => q.Contains("ORDER BY c.sourceName", StringComparison.Ordinal))];
        queries.ShouldNotBeEmpty();
        queries.ShouldAllBe(q => q.Contains("securityTags", StringComparison.Ordinal) && q.Contains("EXISTS", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task An_unrestricted_credential_list_carries_no_reach_predicate()
    {
        CosmosSourceCredentialStore store = await NewCredentialStoreAsync();
        await SeedCredentialAsync(store, "petstore", "production", "acme");

        log.Clear();
        using (SourceCredentialPage page = await store.ListAsync(AccessContext.System, 10, default, default))
        {
            page.Bindings.Count.ShouldBe(1);
        }

        List<string> queries = [.. log.Queries.Where(q => q.Contains("ORDER BY c.sourceName", StringComparison.Ordinal))];
        queries.ShouldNotBeEmpty();
        queries.ShouldAllBe(q => !q.Contains("securityTags", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task A_reach_filtered_credential_count_is_bounded_server_side()
    {
        CosmosSourceCredentialStore store = await NewCredentialStoreAsync();
        await SeedCredentialAsync(store, "petstore", "production", "acme");
        await SeedCredentialAsync(store, "ledger", "production", "globex");

        log.Clear();
        (await store.CountAsync(Scope("acme"), 5, default)).Count.ShouldBe(1);

        List<string> queries = [.. log.Queries.Where(q => q.Contains("OFFSET 0 LIMIT", StringComparison.Ordinal))];
        queries.ShouldNotBeEmpty();
        queries.ShouldAllBe(q => q.Contains("securityTags", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task A_reach_filtered_working_copy_list_sends_the_reach_predicate_to_cosmos()
    {
        CosmosWorkspaceWorkflowStore store = await NewWorkspaceStoreAsync();
        await SeedWorkingCopyAsync(store, "acme");
        await SeedWorkingCopyAsync(store, "globex");

        log.Clear();
        using (WorkspaceWorkflowPage page = await store.ListAsync(Scope("acme"), 10, default, default))
        {
            page.WorkingCopies.Select(w => w.ManagementTagsValue.ToList().Single().Value).ShouldBe(["acme"]);
        }

        List<string> queries = [.. log.Queries.Where(q => q.Contains("ORDER BY c.pk", StringComparison.Ordinal))];
        queries.ShouldNotBeEmpty();
        queries.ShouldAllBe(q => q.Contains("securityTags", StringComparison.Ordinal) && q.Contains("EXISTS", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task An_unrestricted_working_copy_list_carries_no_reach_predicate()
    {
        CosmosWorkspaceWorkflowStore store = await NewWorkspaceStoreAsync();
        await SeedWorkingCopyAsync(store, "acme");

        log.Clear();
        using (WorkspaceWorkflowPage page = await store.ListAsync(AccessContext.System, 10, default, default))
        {
            page.WorkingCopies.Count.ShouldBe(1);
        }

        List<string> queries = [.. log.Queries.Where(q => q.Contains("ORDER BY c.pk", StringComparison.Ordinal))];
        queries.ShouldNotBeEmpty();
        queries.ShouldAllBe(q => !q.Contains("securityTags", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task A_reach_filtered_working_copy_count_is_bounded_server_side()
    {
        CosmosWorkspaceWorkflowStore store = await NewWorkspaceStoreAsync();
        await SeedWorkingCopyAsync(store, "acme");
        await SeedWorkingCopyAsync(store, "globex");

        log.Clear();
        (await store.CountAsync(Scope("acme"), 5, default)).Count.ShouldBe(1);

        List<string> queries = [.. log.Queries.Where(q => q.Contains("OFFSET 0 LIMIT", StringComparison.Ordinal))];
        queries.ShouldNotBeEmpty();
        queries.ShouldAllBe(q => q.Contains("securityTags", StringComparison.Ordinal));
    }

    private static async ValueTask ResetAsync()
    {
        try
        {
            await client.GetDatabase(DatabaseName).DeleteAsync();
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            // Nothing to reset on the first run.
        }
    }

    private static async ValueTask<CosmosEnvironmentStore> NewEnvironmentStoreAsync()
    {
        await ResetAsync();
        await CosmosEnvironmentStore.PrepareAsync(client, DatabaseName);
        return await CosmosEnvironmentStore.ConnectAsync(client, DatabaseName);
    }

    private static async ValueTask<CosmosSourceStore> NewSourceStoreAsync()
    {
        await ResetAsync();
        await CosmosSourceStore.PrepareAsync(client, DatabaseName);
        return await CosmosSourceStore.ConnectAsync(client, DatabaseName);
    }

    private static async ValueTask<CosmosSourceCredentialStore> NewCredentialStoreAsync()
    {
        await ResetAsync();
        await CosmosSourceCredentialStore.PrepareAsync(client, DatabaseName);
        return await CosmosSourceCredentialStore.ConnectAsync(client, DatabaseName);
    }

    private static async ValueTask<CosmosWorkspaceWorkflowStore> NewWorkspaceStoreAsync()
    {
        await ResetAsync();
        await CosmosWorkspaceWorkflowStore.PrepareAsync(client, DatabaseName);
        return await CosmosWorkspaceWorkflowStore.ConnectAsync(client, DatabaseName);
    }

    private static async ValueTask SeedEnvironmentAsync(CosmosEnvironmentStore store, string name, string tenant)
    {
        using ParsedJsonDocument<Environment> draft = Environment.Draft(name, null, null, Tenant(tenant));
        using ParsedJsonDocument<Environment> added = await store.AddAsync(draft.RootElement, "system", default);
    }

    private static async ValueTask SeedSourceAsync(CosmosSourceStore store, string name, string tenant)
    {
        using ParsedJsonDocument<RegisteredSource> draft = RegisteredSource.Draft(name, "openapi", DocUtf8("v1"), null, null, Tenant(tenant));
        using ParsedJsonDocument<RegisteredSource> added = await store.AddAsync(draft.RootElement, "system", default);
    }

    private static async ValueTask SeedCredentialAsync(CosmosSourceCredentialStore store, string sourceName, string environment, string tenant)
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

    private static async ValueTask SeedWorkingCopyAsync(CosmosWorkspaceWorkflowStore store, string tenant)
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

    // Records every seekable request body the client sends and exposes the query bodies ({"query": ...}); the stream
    // is rewound after reading so the pipeline sends it unchanged.
    private sealed class QueryLog : RequestHandler
    {
        private readonly object gate = new();
        private readonly List<string> bodies = [];

        public IReadOnlyList<string> Queries
        {
            get
            {
                lock (this.gate)
                {
                    return [.. this.bodies.Where(static b => b.Contains("\"query\"", StringComparison.Ordinal))];
                }
            }
        }

        public void Clear()
        {
            lock (this.gate)
            {
                this.bodies.Clear();
            }
        }

        public override async Task<ResponseMessage> SendAsync(RequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Content is { CanSeek: true } content)
            {
                long position = content.Position;
                using var reader = new StreamReader(content, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true);
                string body = await reader.ReadToEndAsync(cancellationToken);
                content.Position = position;
                lock (this.gate)
                {
                    this.bodies.Add(body);
                }
            }

            return await base.SendAsync(request, cancellationToken);
        }
    }
}