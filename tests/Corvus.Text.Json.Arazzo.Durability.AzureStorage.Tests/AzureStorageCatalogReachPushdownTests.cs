// <copyright file="AzureStorageCatalogReachPushdownTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Azure.Core;
using Azure.Core.Pipeline;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Corvus.Text.Json.Arazzo.Durability;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;
using Testcontainers.Azurite;

namespace Corvus.Text.Json.Arazzo.Durability.AzureStorage.Tests;

/// <summary>
/// Proves the Azure Storage catalog store narrows a reach-filtered query through its security-label index
/// (design §14.4) instead of scanning the catalog table and discarding rows in process — the catalog sibling of
/// <see cref="AzureStorageReachPushdownTests"/>.
/// </summary>
/// <remarks>
/// Table storage indexes only PartitionKey and RowKey, so "server-side" would not have been enough on its own: a
/// filter on any other property still scans. These tests therefore assert on the requests the store actually
/// issues — that the label table is consulted, and that the catalog rows are then addressed by explicit
/// (partition, row) keys rather than swept. A catalog query URI names the table as <c>arazzocatalog(</c>, which a
/// label lookup (<c>arazzocataloglabels(</c>) never contains, so the two are distinguishable in the log.
/// </remarks>
[TestClass]
[TestCategory("integration")]
[TestCategory("docker")]
public sealed class AzureStorageCatalogReachPushdownTests
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
    public async Task A_reach_filtered_query_resolves_the_label_index_and_addresses_only_those_versions()
    {
        (IWorkflowCatalogStore store, RequestLog log) = await NewStoreAsync();
        await SeedAsync(store, count: 40);

        log.Clear();

        // The reachable bases (globex-*) do NOT lead in (PartitionKey, RowKey) order — acme-* sorts first — so a
        // store that merely swept and took the first page could not satisfy the content assertion by accident.
        using CatalogPage page = await store.QueryAsync(new CatalogQuery(Limit: 5, Security: GlobexReach()), default);

        page.Versions.Count.ShouldBe(5);
        page.Versions.ShouldAllBe(v => v.Ref.BaseWorkflowId.StartsWith("globex-", StringComparison.Ordinal));

        // The label table was consulted: the reach became an indexed partition lookup.
        log.Uris.ShouldContain(u => u.Contains("arazzocataloglabels", StringComparison.OrdinalIgnoreCase));

        // And the catalog rows were then addressed by explicit keys. Without that the query would be a sweep of
        // every tenant's versions, which is the whole finding — a property filter here is not an index.
        string catalogQuery = log.Uris.First(u => u.Contains("arazzocatalog(", StringComparison.OrdinalIgnoreCase));
        Uri.UnescapeDataString(catalogQuery).ShouldContain("RowKey eq");
    }

    [TestMethod]
    public async Task An_unreachable_query_reads_no_versions_at_all()
    {
        (IWorkflowCatalogStore store, RequestLog log) = await NewStoreAsync();
        await SeedAsync(store, count: 8);

        log.Clear();
        using CatalogPage page = await store.QueryAsync(new CatalogQuery(Limit: 5, Security: Reach("tenant == 'nobody'")), default);

        page.Versions.ShouldBeEmpty();

        // An empty candidate set is not "no narrowing": the store must answer without touching the catalog table.
        log.Uris.ShouldNotContain(u => u.Contains("arazzocatalog(", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public async Task An_unfiltered_query_still_sweeps_rather_than_enumerating_ids()
    {
        // The negative control for the first test: narrowing must be driven by the reach, not applied always.
        (IWorkflowCatalogStore store, RequestLog log) = await NewStoreAsync();
        await SeedAsync(store, count: 8);

        log.Clear();
        using CatalogPage page = await store.QueryAsync(new CatalogQuery(Limit: 5), default);

        string catalogQuery = log.Uris.First(u => u.Contains("arazzocatalog(", StringComparison.OrdinalIgnoreCase));
        Uri.UnescapeDataString(catalogQuery).ShouldNotContain("RowKey eq");
    }

    [TestMethod]
    public async Task Deleting_a_version_removes_its_label_entries()
    {
        (IWorkflowCatalogStore store, RequestLog log) = await NewStoreAsync();

        (await store.AddAsync("secure-flow", Package("secure-flow"), Meta("acme"), default)).Dispose();
        using (CatalogPage page = await store.QueryAsync(new CatalogQuery(Limit: 10, Security: AcmeReach()), default))
        {
            page.Versions.Count.ShouldBe(1);
        }

        (await store.DeleteAsync("secure-flow", 1, default)).ShouldBeTrue();

        // A version recreated under the same (base, number) must not inherit the deleted version's labels: after
        // the delete the acme entry is gone, so an acme-reach query answers empty from the index alone, without
        // reading the catalog table the recreated globex version now occupies.
        (await store.AddAsync("secure-flow", Package("secure-flow"), Meta("globex"), default)).Dispose();
        log.Clear();
        using (CatalogPage page = await store.QueryAsync(new CatalogQuery(Limit: 10, Security: AcmeReach()), default))
        {
            page.Versions.ShouldBeEmpty();
        }

        log.Uris.ShouldNotContain(u => u.Contains("arazzocatalog(", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public async Task Retagging_a_version_moves_its_label_entries()
    {
        // The catalog-specific write path the run store does not have: a §14.2 re-tag replaces the version's
        // security tags in place, so the label diff must be maintained — the new tenant's entry added (or the row
        // is hidden from its rightful reach, an availability failure) and the old tenant's removed.
        (IWorkflowCatalogStore store, RequestLog log) = await NewStoreAsync();

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

        log.Clear();
        using (CatalogPage page = await store.QueryAsync(new CatalogQuery(Limit: 10, Security: AcmeReach()), default))
        {
            page.Versions.ShouldBeEmpty();
        }

        log.Uris.ShouldNotContain(u => u.Contains("arazzocatalog(", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public async Task A_tag_value_carrying_table_reserved_characters_round_trips()
    {
        // Labels are user-authored and the partition key forbids / \ # ? and control characters, so the encoding is
        // what stops a crafted tag from being rejected by the service or colliding with another tag's partition.
        (IWorkflowCatalogStore store, _) = await NewStoreAsync();

        (await store.AddAsync("odd-flow", Package("odd-flow"), Meta("a#b?c"), default)).Dispose();

        using CatalogPage page = await store.QueryAsync(new CatalogQuery(Limit: 10, Security: Reach("tenant == 'a#b?c'")), default);
        page.Versions.Count.ShouldBe(1);
    }

    private static SecurityFilter AcmeReach() => Reach("tenant == 'acme'");

    private static SecurityFilter GlobexReach() => Reach("tenant == 'globex'");

    private static SecurityFilter Reach(string rule)
        => new([SecurityRule.Compile(rule)], new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal));

    private static CatalogMetadata Meta(string tenant)
        => new(new CatalogOwner("Team A", "team-a@example.com"), "alice", default, SecurityTagSet.FromTags([new("tenant", tenant)]));

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

    private static async ValueTask SeedAsync(IWorkflowCatalogStore store, int count)
    {
        for (int i = 0; i < count / 2; ++i)
        {
            (await store.AddAsync($"acme-{i:D3}", Package($"acme-{i:D3}"), Meta("acme"), default)).Dispose();
            (await store.AddAsync($"globex-{i:D3}", Package($"globex-{i:D3}"), Meta("globex"), default)).Dispose();
        }
    }

    private static async ValueTask<(IWorkflowCatalogStore Store, RequestLog Log)> NewStoreAsync()
    {
        string connectionString = container.GetConnectionString();
        var log = new RequestLog();

        var tableOptions = new TableClientOptions();
        tableOptions.AddPolicy(log, HttpPipelinePosition.PerCall);

        var blobService = new BlobServiceClient(connectionString, new BlobClientOptions(BlobClientOptions.ServiceVersion.V2024_11_04));
        var tableService = new TableServiceClient(connectionString, tableOptions);

        // Provision, then clear the data, matching the conformance fixture: dropping and recreating a table is
        // eventually consistent in Azure Storage and makes the next test flaky.
        var admin = new TableServiceClient(connectionString);
        await AzureStorageWorkflowCatalogStore.PrepareAsync(blobService, admin);

        BlobContainerClient packages = blobService.GetBlobContainerClient("arazzo-catalog");
        await foreach (Azure.Storage.Blobs.Models.BlobItem blob in packages.GetBlobsAsync())
        {
            await packages.DeleteBlobIfExistsAsync(blob.Name);
        }

        foreach (string table in (string[])["arazzocatalog", "arazzocataloglabels"])
        {
            TableClient client = admin.GetTableClient(table);
            await foreach (TableEntity entity in client.QueryAsync<TableEntity>())
            {
                await client.DeleteEntityAsync(entity.PartitionKey, entity.RowKey);
            }
        }

        return (await AzureStorageWorkflowCatalogStore.ConnectAsync(blobService, tableService), log);
    }

    // Records the request line of every table call, which is where a query's filter travels.
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
