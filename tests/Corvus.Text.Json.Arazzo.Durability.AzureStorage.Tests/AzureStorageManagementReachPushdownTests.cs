// <copyright file="AzureStorageManagementReachPushdownTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Azure.Core;
using Azure.Core.Pipeline;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Environments;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Corvus.Text.Json.Arazzo.Durability.Sources;
using Corvus.Text.Json.Arazzo.Durability.WorkspaceWorkflows;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;
using Testcontainers.Azurite;
using Environment = Corvus.Text.Json.Arazzo.Durability.Environments.Environment;

namespace Corvus.Text.Json.Arazzo.Durability.AzureStorage.Tests;

/// <summary>
/// Proves the four Azure Storage management stores (environment, source, source-credential, workspace-workflow)
/// narrow a reach-filtered list/count through their §14.4 label tables — the reach resolving to candidate entity
/// keys by indexed partition lookups — instead of sweeping the store table and discarding rows in process, and
/// that a re-tagging update re-points the label entries around the write. The management sibling of
/// <see cref="AzureStorageCatalogReachPushdownTests"/>.
/// </summary>
/// <remarks>
/// Table storage indexes only PartitionKey and RowKey, so these tests assert on the requests the store actually
/// issues: a sweep is a bare table query (<c>{table}()</c>), an addressed candidate read names its keys
/// (<c>{table}(PartitionKey=</c>), and the label resolution queries the label table. The label table's name never
/// contains the store table's query form, so the two are distinguishable in the log.
/// </remarks>
[TestClass]
[TestCategory("integration")]
[TestCategory("docker")]
public sealed class AzureStorageManagementReachPushdownTests
{
    private static AzuriteContainer container = null!;

    [ClassInitialize]
    public static async Task ClassInitAsync(TestContext context)
    {
        container = new AzuriteBuilder()
            .WithImage("mcr.microsoft.com/azure-storage/azurite:latest")
            .Build();
        await container.StartAsync();
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
    public async Task A_reach_filtered_environment_list_resolves_the_label_table_and_addresses_only_those_rows()
    {
        (IEnvironmentStore store, RequestLog log) = await NewEnvironmentStoreAsync();
        await SeedEnvironmentsAsync(store);

        log.Clear();

        // The reachable rows (globex-*) do NOT lead in keyset order — acme-* sorts first — so a store that merely
        // swept and took the first matching page would read the acme rows on the way and the read bound below
        // could not hold by accident.
        using (EnvironmentPage page = await store.ListAsync(Scope("globex"), 5, default, default))
        {
            page.Environments.Select(e => e.ManagementTagsValue.ToList().Single().Value).ShouldBe(["globex", "globex"]);
        }

        // The label table was consulted: the reach became indexed partition lookups...
        log.Uris.ShouldContain(u => IsLabelTraffic(u, "arazzoEnvironmentLabels"));

        // ...the store table was never swept...
        log.Uris.ShouldNotContain(u => IsSweep(u, "arazzoEnvironments"));

        // ...and only candidate rows were addressed.
        log.Uris.Count(u => IsAddressedRead(u, "arazzoEnvironments")).ShouldBeLessThanOrEqualTo(2);
    }

    [TestMethod]
    public async Task An_unreachable_environment_list_reads_no_rows_at_all()
    {
        (IEnvironmentStore store, RequestLog log) = await NewEnvironmentStoreAsync();
        await SeedEnvironmentsAsync(store);

        log.Clear();
        using (EnvironmentPage page = await store.ListAsync(Scope("nobody"), 5, default, default))
        {
            page.Environments.ShouldBeEmpty();
        }

        // An empty candidate set is not "no narrowing": the store must answer without touching its table.
        log.Uris.ShouldNotContain(u => IsAddressedRead(u, "arazzoEnvironments"));
        log.Uris.ShouldNotContain(u => IsSweep(u, "arazzoEnvironments"));
    }

    [TestMethod]
    public async Task An_unrestricted_environment_list_still_sweeps_rather_than_enumerating_labels()
    {
        // The negative control for the first test: narrowing must be driven by the reach, not applied always.
        (IEnvironmentStore store, RequestLog log) = await NewEnvironmentStoreAsync();
        await SeedEnvironmentsAsync(store);

        log.Clear();
        using (EnvironmentPage page = await store.ListAsync(AccessContext.System, 10, default, default))
        {
            page.Environments.Count.ShouldBe(4);
        }

        log.Uris.ShouldContain(u => IsSweep(u, "arazzoEnvironments"));
        log.Uris.ShouldNotContain(u => IsLabelTraffic(u, "arazzoEnvironmentLabels"));
    }

    [TestMethod]
    public async Task A_re_tagging_environment_update_re_points_the_label_entries()
    {
        // A §14.2 re-tag replaces the row's management tags in place, so the label diff must be maintained — the
        // new tenant's entry added (or the row is hidden from its rightful reach, an availability failure) and the
        // old tenant's removed. The stale old entry would be discarded by the exact evaluation, so the wire is
        // what the test observes: the old scope's narrowed list must answer empty without addressing a row.
        (IEnvironmentStore store, RequestLog log) = await NewEnvironmentStoreAsync();

        using (ParsedJsonDocument<Environment> draft = Environment.Draft("production", null, null, Tenant("acme")))
        {
            (await store.AddAsync(draft.RootElement, "system", default)).Dispose();
        }

        using (ParsedJsonDocument<Environment> reTag = Environment.Draft("production", null, null, Tenant("globex")))
        using (ParsedJsonDocument<Environment>? updated = await store.UpdateAsync("production", reTag.RootElement, WorkflowEtag.None, "carol", AccessContext.System, default))
        {
            updated.ShouldNotBeNull();
        }

        using (EnvironmentPage page = await store.ListAsync(Scope("globex"), 5, default, default))
        {
            page.Environments.Count.ShouldBe(1);
        }

        log.Clear();
        using (EnvironmentPage page = await store.ListAsync(Scope("acme"), 5, default, default))
        {
            page.Environments.ShouldBeEmpty();
        }

        log.Uris.ShouldNotContain(u => IsAddressedRead(u, "arazzoEnvironments"));
    }

    [TestMethod]
    public async Task Deleting_an_environment_removes_its_label_entries()
    {
        (IEnvironmentStore store, RequestLog log) = await NewEnvironmentStoreAsync();

        using (ParsedJsonDocument<Environment> draft = Environment.Draft("ephemeral", null, null, Tenant("acme")))
        {
            (await store.AddAsync(draft.RootElement, "system", default)).Dispose();
        }

        (await store.DeleteAsync("ephemeral", WorkflowEtag.None, AccessContext.System, default)).ShouldBeTrue();

        // A stale entry would produce the same empty page while still costing the read, so the wire is what the
        // test observes.
        log.Clear();
        using (EnvironmentPage page = await store.ListAsync(Scope("acme"), 5, default, default))
        {
            page.Environments.ShouldBeEmpty();
        }

        log.Uris.ShouldNotContain(u => IsAddressedRead(u, "arazzoEnvironments"));
    }

    [TestMethod]
    public async Task A_reach_filtered_environment_count_narrows_before_reading()
    {
        (IEnvironmentStore store, RequestLog log) = await NewEnvironmentStoreAsync();
        await SeedEnvironmentsAsync(store);

        log.Clear();
        (await store.CountAsync(Scope("globex"), 100, default)).ShouldBe((2, false));

        log.Uris.ShouldContain(u => IsLabelTraffic(u, "arazzoEnvironmentLabels"));
        log.Uris.ShouldNotContain(u => IsSweep(u, "arazzoEnvironments"));
        log.Uris.Count(u => IsAddressedRead(u, "arazzoEnvironments")).ShouldBeLessThanOrEqualTo(2);
    }

    [TestMethod]
    public async Task A_reach_filtered_source_list_resolves_the_label_table_and_addresses_only_those_rows()
    {
        (ISourceStore store, RequestLog log) = await NewSourceStoreAsync();
        await SeedSourcesAsync(store);

        log.Clear();
        using (SourcePage page = await store.ListAsync(Scope("globex"), 5, default, default))
        {
            page.Sources.Select(s => s.ManagementTagsValue.ToList().Single().Value).ShouldBe(["globex", "globex"]);
        }

        log.Uris.ShouldContain(u => IsLabelTraffic(u, "arazzoSourceLabels"));
        log.Uris.ShouldNotContain(u => IsSweep(u, "arazzoSources"));
        log.Uris.Count(u => IsAddressedRead(u, "arazzoSources")).ShouldBeLessThanOrEqualTo(2);
    }

    [TestMethod]
    public async Task A_reach_filtered_source_count_narrows_before_reading()
    {
        (ISourceStore store, RequestLog log) = await NewSourceStoreAsync();
        await SeedSourcesAsync(store);

        log.Clear();
        (await store.CountAsync(Scope("globex"), 100, default)).ShouldBe((2, false));

        log.Uris.ShouldContain(u => IsLabelTraffic(u, "arazzoSourceLabels"));
        log.Uris.ShouldNotContain(u => IsSweep(u, "arazzoSources"));
        log.Uris.Count(u => IsAddressedRead(u, "arazzoSources")).ShouldBeLessThanOrEqualTo(2);
    }

    [TestMethod]
    public async Task A_reach_filtered_credential_list_resolves_the_label_table_and_addresses_only_those_rows()
    {
        (ISourceCredentialStore store, RequestLog log) = await NewCredentialStoreAsync();
        await SeedCredentialsAsync(store);

        log.Clear();
        using (SourceCredentialPage page = await store.ListAsync(Scope("globex"), 5, default, default))
        {
            page.Bindings.Select(b => b.ManagementTagsValue.ToList().Single().Value).ShouldBe(["globex", "globex"]);
        }

        log.Uris.ShouldContain(u => IsLabelTraffic(u, "arazzoSourceCredentialLabels"));
        log.Uris.ShouldNotContain(u => IsSweep(u, "arazzoSourceCredentials"));
        log.Uris.Count(u => IsAddressedRead(u, "arazzoSourceCredentials")).ShouldBeLessThanOrEqualTo(2);
    }

    [TestMethod]
    public async Task A_reach_filtered_credential_count_narrows_before_reading()
    {
        (ISourceCredentialStore store, RequestLog log) = await NewCredentialStoreAsync();
        await SeedCredentialsAsync(store);

        log.Clear();
        (await store.CountAsync(Scope("globex"), 100, default)).ShouldBe((2, false));

        log.Uris.ShouldContain(u => IsLabelTraffic(u, "arazzoSourceCredentialLabels"));
        log.Uris.ShouldNotContain(u => IsSweep(u, "arazzoSourceCredentials"));
        log.Uris.Count(u => IsAddressedRead(u, "arazzoSourceCredentials")).ShouldBeLessThanOrEqualTo(2);
    }

    [TestMethod]
    public async Task A_reach_filtered_working_copy_list_resolves_the_label_table_and_addresses_only_those_rows()
    {
        (IWorkspaceWorkflowStore store, RequestLog log) = await NewWorkspaceStoreAsync();
        await SeedWorkingCopiesAsync(store);

        log.Clear();
        using (WorkspaceWorkflowPage page = await store.ListAsync(Scope("globex"), 5, default, default))
        {
            page.WorkingCopies.Select(w => w.ManagementTagsValue.ToList().Single().Value).ShouldBe(["globex", "globex"]);
        }

        log.Uris.ShouldContain(u => IsLabelTraffic(u, "arazzoWorkspaceWorkflowLabels"));
        log.Uris.ShouldNotContain(u => IsSweep(u, "arazzoWorkspaceWorkflows"));
        log.Uris.Count(u => IsAddressedRead(u, "arazzoWorkspaceWorkflows")).ShouldBeLessThanOrEqualTo(2);
    }

    [TestMethod]
    public async Task A_reach_filtered_working_copy_count_narrows_before_reading()
    {
        (IWorkspaceWorkflowStore store, RequestLog log) = await NewWorkspaceStoreAsync();
        await SeedWorkingCopiesAsync(store);

        log.Clear();
        (await store.CountAsync(Scope("globex"), 100, default)).ShouldBe((2, false));

        log.Uris.ShouldContain(u => IsLabelTraffic(u, "arazzoWorkspaceWorkflowLabels"));
        log.Uris.ShouldNotContain(u => IsSweep(u, "arazzoWorkspaceWorkflows"));
        log.Uris.Count(u => IsAddressedRead(u, "arazzoWorkspaceWorkflows")).ShouldBeLessThanOrEqualTo(2);
    }

    // A bare table query (the sweep) is "{table}()"; an addressed read names its keys. The label table's name
    // extends the store table's in two of the four pairs, so the matchers require the query form immediately
    // after the full table name.
    private static bool IsSweep(string uri, string table)
        => uri.Contains(table + "()", StringComparison.OrdinalIgnoreCase);

    private static bool IsAddressedRead(string uri, string table)
        => uri.Contains(table + "(PartitionKey=", StringComparison.OrdinalIgnoreCase);

    private static bool IsLabelTraffic(string uri, string labelsTable)
        => uri.Contains(labelsTable + "(", StringComparison.OrdinalIgnoreCase);

    // Two rows per tenant, with the acme names leading the keyset order so a globex-scoped page cannot satisfy its
    // read bound by accident of ordering.
    private static async ValueTask SeedEnvironmentsAsync(IEnvironmentStore store)
    {
        foreach ((string name, string tenant) in ((string, string)[])[("acme-0", "acme"), ("acme-1", "acme"), ("globex-0", "globex"), ("globex-1", "globex")])
        {
            using ParsedJsonDocument<Environment> draft = Environment.Draft(name, null, null, Tenant(tenant));
            (await store.AddAsync(draft.RootElement, "system", default)).Dispose();
        }
    }

    private static async ValueTask SeedSourcesAsync(ISourceStore store)
    {
        foreach ((string name, string tenant) in ((string, string)[])[("acme-0", "acme"), ("acme-1", "acme"), ("globex-0", "globex"), ("globex-1", "globex")])
        {
            using ParsedJsonDocument<RegisteredSource> draft = RegisteredSource.Draft(name, "openapi", DocUtf8(name), null, null, Tenant(tenant));
            (await store.AddAsync(draft.RootElement, "system", default)).Dispose();
        }
    }

    private static async ValueTask SeedCredentialsAsync(ISourceCredentialStore store)
    {
        foreach ((string source, string tenant) in ((string, string)[])[("acme-api-0", "acme"), ("acme-api-1", "acme"), ("globex-api-0", "globex"), ("globex-api-1", "globex")])
        {
            var definition = new SourceCredentialDefinition(
                source,
                "production",
                SourceCredentialKind.ApiKey,
                [new SecretReferenceDefinition("value", $"keyvault://{source}-apikey")],
                ManagementTags: Tenant(tenant),
                UsageTags: Tenant(tenant));
            using ParsedJsonDocument<SourceCredentialBinding> added = await store.AddAsync(definition, "system", default);
        }
    }

    private static async ValueTask SeedWorkingCopiesAsync(IWorkspaceWorkflowStore store)
    {
        foreach (string tenant in (string[])["acme", "acme", "globex", "globex"])
        {
            using ParsedJsonDocument<WorkspaceWorkflow> draft = WorkspaceWorkflow.Draft("wc", DocUtf8(tenant), default, null, null, Tenant(tenant));
            (await store.AddAsync(draft.RootElement, "system", default)).Dispose();
        }
    }

    private static ReadOnlyMemory<byte> DocUtf8(string marker)
        => Encoding.UTF8.GetBytes($$"""{"arazzo":"1.1.0","x-marker":"{{marker}}"}""");

    private static SecurityTagSet Tenant(string tenant) => SecurityTagSet.FromTags([new SecurityTag("tenant", tenant)]);

    // A read/write/purge reach that admits exactly the rows tagged tenant=<tenant> (tenant == $claim.tenant resolved
    // against a single-tenant claim).
    private static AccessContext Scope(string tenant) => AccessContext.Uniform(
        new SecurityFilter([SecurityRule.Compile("tenant == $claim.tenant")], new Dictionary<string, IReadOnlyList<string>> { ["tenant"] = [tenant] }));

    private static async ValueTask<(TableServiceClient Logged, TableServiceClient Admin, RequestLog Log)> NewTableServicesAsync(params string[] tables)
    {
        string connectionString = container.GetConnectionString();
        var log = new RequestLog();
        var tableOptions = new TableClientOptions();
        tableOptions.AddPolicy(log, HttpPipelinePosition.PerCall);
        var logged = new TableServiceClient(connectionString, tableOptions);
        var admin = new TableServiceClient(connectionString);

        // Provision, then clear the data, matching the conformance fixture: dropping and recreating a table is
        // eventually consistent in Azure Storage and makes the next test flaky.
        foreach (string table in tables)
        {
            TableClient client = admin.GetTableClient(table);
            await client.CreateIfNotExistsAsync();
            await foreach (TableEntity entity in client.QueryAsync<TableEntity>())
            {
                await client.DeleteEntityAsync(entity.PartitionKey, entity.RowKey, Azure.ETag.All);
            }
        }

        return (logged, admin, log);
    }

    private static async ValueTask<(IEnvironmentStore Store, RequestLog Log)> NewEnvironmentStoreAsync()
    {
        (TableServiceClient logged, TableServiceClient admin, RequestLog log) = await NewTableServicesAsync("arazzoEnvironments", "arazzoEnvironmentTenancy", "arazzoEnvironmentLabels");
        await AzureStorageEnvironmentStore.PrepareAsync(admin);
        return (await AzureStorageEnvironmentStore.ConnectAsync(logged), log);
    }

    private static async ValueTask<(ISourceStore Store, RequestLog Log)> NewSourceStoreAsync()
    {
        (TableServiceClient logged, TableServiceClient admin, RequestLog log) = await NewTableServicesAsync("arazzoSources", "arazzoSourceLabels");
        await AzureStorageSourceStore.PrepareAsync(admin);
        return (await AzureStorageSourceStore.ConnectAsync(logged), log);
    }

    private static async ValueTask<(ISourceCredentialStore Store, RequestLog Log)> NewCredentialStoreAsync()
    {
        (TableServiceClient logged, TableServiceClient admin, RequestLog log) = await NewTableServicesAsync("arazzoSourceCredentials", "arazzoSourceCredentialLabels");
        await AzureStorageSourceCredentialStore.PrepareAsync(admin);
        return (await AzureStorageSourceCredentialStore.ConnectAsync(logged), log);
    }

    private static async ValueTask<(IWorkspaceWorkflowStore Store, RequestLog Log)> NewWorkspaceStoreAsync()
    {
        string connectionString = container.GetConnectionString();
        (TableServiceClient logged, TableServiceClient admin, RequestLog log) = await NewTableServicesAsync("arazzoWorkspaceWorkflows", "arazzoWorkspaceWorkflowLabels");
        var blobService = new BlobServiceClient(connectionString, new BlobClientOptions(BlobClientOptions.ServiceVersion.V2024_11_04));
        await AzureStorageWorkspaceWorkflowStore.PrepareAsync(blobService, admin);

        BlobContainerClient blobs = blobService.GetBlobContainerClient("arazzo-workspace-workflows");
        await foreach (Azure.Storage.Blobs.Models.BlobItem blob in blobs.GetBlobsAsync())
        {
            await blobs.DeleteBlobIfExistsAsync(blob.Name);
        }

        return (await AzureStorageWorkspaceWorkflowStore.ConnectAsync(blobService, logged), log);
    }

    // Records the request line of every table call, which is where a query's shape travels.
    private sealed class RequestLog : HttpPipelineSynchronousPolicy
    {
        private readonly List<string> uris = [];

        public IReadOnlyList<string> Uris
        {
            get
            {
                lock (this.uris)
                {
                    return [.. this.uris];
                }
            }
        }

        public void Clear()
        {
            lock (this.uris)
            {
                this.uris.Clear();
            }
        }

        public override void OnSendingRequest(HttpMessage message)
        {
            lock (this.uris)
            {
                this.uris.Add(message.Request.Uri.ToString());
            }
        }
    }
}
