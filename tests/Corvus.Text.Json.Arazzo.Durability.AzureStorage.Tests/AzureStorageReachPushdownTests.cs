// <copyright file="AzureStorageReachPushdownTests.cs" company="Endjin Limited">
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
/// Proves the Azure Storage store narrows a reach-filtered query through its security-label index (design §14.4)
/// instead of scanning the run partition and discarding rows in process.
/// </summary>
/// <remarks>
/// Table storage indexes only PartitionKey and RowKey, so "server-side" would not have been enough on its own: a
/// filter on any other property still scans. These tests therefore assert on the requests the store actually
/// issues — that the label table is consulted, and that the run partition is then addressed by explicit row keys
/// rather than swept.
/// </remarks>
[TestClass]
[TestCategory("integration")]
[TestCategory("docker")]
public sealed class AzureStorageReachPushdownTests
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
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
    public async Task A_reach_filtered_query_resolves_the_label_index_and_addresses_only_those_runs()
    {
        (IWorkflowStateStore store, RequestLog log) = await NewStoreAsync();
        await SeedAsync(store, count: 40);

        log.Clear();
        WorkflowRunPage page = await ((IWorkflowWaitIndex)store).QueryAsync(
            new WorkflowQuery(Limit: 5, Security: AcmeReach()), default);

        page.Runs.Count.ShouldBe(5);
        page.Runs.ShouldAllBe(r => r.Id.Value.StartsWith("acme-", StringComparison.Ordinal));

        // The label table was consulted: the reach became an indexed partition lookup.
        log.Uris.ShouldContain(u => u.Contains("arazzolabels", StringComparison.OrdinalIgnoreCase));

        // And the run partition was then addressed by explicit row keys. Without that the query would be a sweep
        // of every tenant's runs, which is the whole finding — a property filter here is not an index.
        string runQuery = log.Uris.First(u => u.Contains("arazzoindex", StringComparison.OrdinalIgnoreCase));
        Uri.UnescapeDataString(runQuery).ShouldContain("RowKey eq");
    }

    [TestMethod]
    public async Task An_unreachable_query_reads_no_runs_at_all()
    {
        (IWorkflowStateStore store, RequestLog log) = await NewStoreAsync();
        await SeedAsync(store, count: 40);

        log.Clear();
        WorkflowRunPage page = await ((IWorkflowWaitIndex)store).QueryAsync(
            new WorkflowQuery(Limit: 5, Security: Reach("tenant == 'nobody'")), default);

        page.Runs.ShouldBeEmpty();

        // An empty candidate set is not "no narrowing": the store must answer without touching the run table.
        log.Uris.ShouldNotContain(u => u.Contains("arazzoindex", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public async Task An_unfiltered_query_still_sweeps_rather_than_enumerating_ids()
    {
        // The negative control for the first test: narrowing must be driven by the reach, not applied always.
        (IWorkflowStateStore store, RequestLog log) = await NewStoreAsync();
        await SeedAsync(store, count: 8);

        log.Clear();
        await ((IWorkflowWaitIndex)store).QueryAsync(new WorkflowQuery(Limit: 5), default);

        string runQuery = log.Uris.First(u => u.Contains("arazzoindex", StringComparison.OrdinalIgnoreCase));
        Uri.UnescapeDataString(runQuery).ShouldNotContain("RowKey eq");
    }

    [TestMethod]
    public async Task Deleting_a_run_removes_its_label_entries()
    {
        (IWorkflowStateStore store, _) = await NewStoreAsync();
        var index = (IWorkflowWaitIndex)store;

        await store.SaveAsync(A("acme-0"), Bytes(), Secured([new("tenant", "acme")]), WorkflowEtag.None, default);
        (await index.QueryAsync(new WorkflowQuery(Limit: 10, Security: AcmeReach()), default)).Runs.Count.ShouldBe(1);

        await store.DeleteAsync(A("acme-0"), default);

        // A run recreated under the same id must not inherit the deleted run's labels, and the stale entry must not
        // resurrect it: the label index is torn down with the row.
        await store.SaveAsync(A("acme-0"), Bytes(), Secured([new("tenant", "globex")]), WorkflowEtag.None, default);
        (await index.QueryAsync(new WorkflowQuery(Limit: 10, Security: AcmeReach()), default)).Runs.ShouldBeEmpty();
    }

    [TestMethod]
    public async Task A_tag_key_carrying_table_reserved_characters_round_trips()
    {
        // Labels are user-authored and the partition key forbids / \ # ? and control characters, so the encoding is
        // what stops a crafted tag from being rejected by the service or colliding with another tag's partition.
        (IWorkflowStateStore store, _) = await NewStoreAsync();
        var index = (IWorkflowWaitIndex)store;

        await store.SaveAsync(A("odd-0"), Bytes(), Secured([new("tenant", "a#b?c")]), WorkflowEtag.None, default);

        (await index.QueryAsync(new WorkflowQuery(Limit: 10, Security: Reach("tenant == 'a#b?c'")), default))
            .Runs.Count.ShouldBe(1);
    }

    private static SecurityFilter AcmeReach() => Reach("tenant == 'acme'");

    private static SecurityFilter Reach(string rule)
        => new([SecurityRule.Compile(rule)], new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal));

    private static ReadOnlyMemory<byte> Bytes() => Encoding.UTF8.GetBytes("{}");

    private static WorkflowRunIndexEntry Secured(SecurityTag[] securityTags)
        => new("wf", WorkflowRunStatus.Running, T0, T0, SecurityTags: SecurityTagSet.FromTags(securityTags));

    // The run's full (environment, runId) address (ADR 0065 decision 9).
    private static WorkflowRunAddress A(string runId) => new("development", new WorkflowRunId(runId));

    // Ids are prefixed by tenant so the reachable ones do NOT lead in row-key order — a store that simply took the
    // first page of a sweep would still pass the count assertion, so the ordering has to make that impossible.
    private static async ValueTask SeedAsync(IWorkflowStateStore store, int count)
    {
        for (int i = 0; i < count / 2; ++i)
        {
            await store.SaveAsync(A($"acme-{i:D3}"), Bytes(), Secured([new("tenant", "acme")]), WorkflowEtag.None, default);
            await store.SaveAsync(A($"globex-{i:D3}"), Bytes(), Secured([new("tenant", "globex")]), WorkflowEtag.None, default);
        }
    }

    private static async ValueTask<(IWorkflowStateStore Store, RequestLog Log)> NewStoreAsync()
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
        await AzureStorageWorkflowStateStore.PrepareAsync(blobService, admin);

        BlobContainerClient runs = blobService.GetBlobContainerClient("arazzo-runs");
        await foreach (Azure.Storage.Blobs.Models.BlobItem blob in runs.GetBlobsAsync())
        {
            await runs.DeleteBlobIfExistsAsync(blob.Name);
        }

        foreach (string table in (string[])["arazzoindex", "arazzoleases", "arazzolabels"])
        {
            TableClient client = admin.GetTableClient(table);
            await foreach (TableEntity entity in client.QueryAsync<TableEntity>())
            {
                await client.DeleteEntityAsync(entity.PartitionKey, entity.RowKey);
            }
        }

        return (await AzureStorageWorkflowStateStore.ConnectAsync(blobService, tableService), log);
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