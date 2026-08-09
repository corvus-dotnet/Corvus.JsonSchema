// <copyright file="AzureStorageObservedIdentityReachPushdownTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Azure.Core;
using Azure.Core.Pipeline;
using Azure.Data.Tables;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;
using Testcontainers.Azurite;

namespace Corvus.Text.Json.Arazzo.Durability.AzureStorage.Tests;

/// <summary>
/// Proves the Azure Storage observed-identity store narrows a reach-scoped search through its security-label
/// entities (design §14.4) instead of listing every primary entity and discarding documents in process — the
/// observed-identity sibling of <see cref="AzureStorageReachPushdownTests"/>.
/// </summary>
/// <remarks>
/// Everything lives in one table, so the request URIs discriminate by their query shapes: a label lookup filters
/// <c>PartitionKey eq 'v~…'</c>, the unnarrowed listing filters <c>PartitionKey ge '~'</c>, and a document read
/// is a point get naming both keys.
/// </remarks>
[TestClass]
[TestCategory("integration")]
[TestCategory("docker")]
public sealed class AzureStorageObservedIdentityReachPushdownTests
{
    private static readonly ObservedIdentity.GranteeKind Team = ObservedIdentity.GranteeKind.EnumValues.Team;
    private static readonly ObservedIdentity.GranteeKind AllKinds = default;
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
    public async Task A_reach_scoped_search_resolves_the_label_entities_and_reads_only_those_identities()
    {
        (IObservedIdentityStore store, RequestLog log) = await NewStoreAsync();
        for (int i = 0; i < 20; ++i)
        {
            await store.SeenAsync(Team, Str($"acme-{i:D3}"), default, Tags("acme"), true, "test", default);
            await store.SeenAsync(Team, Str($"globex-{i:D3}"), default, Tags("globex"), true, "test", default);
        }

        log.Clear();

        // The reachable identities (globex-*) do NOT lead in subject order — acme-* sorts first — so a store
        // that merely listed and took the first matching page could not satisfy the read bound by accident.
        using ObservedIdentityPage page = await store.SearchAsync(ScopeBy("tenant", "globex"), AllKinds, Str(string.Empty), 5, default, default);
        List<string> uris = log.Uris.Select(Uri.UnescapeDataString).ToList();

        page.Identities.Count.ShouldBe(5);
        page.Identities.ShouldAllBe(i => i.SubjectValueValue.StartsWith("globex-", StringComparison.Ordinal));

        // The label partitions were consulted: the reach became indexed partition lookups...
        uris.ShouldContain(u => IsLabelLookup(u));

        // ...the primary partitions were never listed wholesale...
        uris.ShouldNotContain(u => IsFullListing(u));

        // ...and only candidate documents were read. A listing would have read the 20 acme documents too.
        uris.Count(IsDocumentRead).ShouldBeLessThanOrEqualTo(20);
    }

    [TestMethod]
    public async Task An_unreachable_search_reads_no_identities_at_all()
    {
        (IObservedIdentityStore store, RequestLog log) = await NewStoreAsync();
        await store.SeenAsync(Team, Str("acme-team"), default, Tags("acme"), true, "test", default);

        log.Clear();
        using ObservedIdentityPage page = await store.SearchAsync(ScopeBy("tenant", "nobody"), AllKinds, Str(string.Empty), 5, default, default);
        List<string> uris = log.Uris.Select(Uri.UnescapeDataString).ToList();

        page.Identities.Count.ShouldBe(0);

        // An empty candidate set is not "no narrowing": the store must answer without reading a single document.
        uris.ShouldNotContain(u => IsDocumentRead(u));
        uris.ShouldNotContain(u => IsFullListing(u));
    }

    [TestMethod]
    public async Task An_unrestricted_search_still_lists_rather_than_enumerating_labels()
    {
        // The negative control: narrowing must be driven by the reach, not applied always.
        (IObservedIdentityStore store, RequestLog log) = await NewStoreAsync();
        await store.SeenAsync(Team, Str("acme-team"), default, Tags("acme"), true, "test", default);

        log.Clear();
        using ObservedIdentityPage page = await store.SearchAsync(AccessContext.System, AllKinds, Str(string.Empty), 5, default, default);
        List<string> uris = log.Uris.Select(Uri.UnescapeDataString).ToList();

        uris.ShouldContain(u => IsFullListing(u));
        uris.ShouldNotContain(u => IsLabelLookup(u));
    }

    [TestMethod]
    public async Task A_re_sighting_that_changes_the_tags_moves_the_label_entities()
    {
        // The wire half of the conformance suite's visibility test: after the re-homing, the old tenant's search
        // answers empty from the label entities alone, without reading the document the identity still occupies —
        // a stale entry would produce the same empty page while still costing the read.
        (IObservedIdentityStore store, RequestLog log) = await NewStoreAsync();
        await store.SeenAsync(Team, Str("mobile"), default, Tags("acme"), true, "test", default);
        await store.SeenAsync(Team, Str("mobile"), default, Tags("globex"), true, "test", default);

        using (ObservedIdentityPage globex = await store.SearchAsync(ScopeBy("tenant", "globex"), AllKinds, Str(string.Empty), 5, default, default))
        {
            globex.Identities.Count.ShouldBe(1);
        }

        log.Clear();
        using (ObservedIdentityPage acme = await store.SearchAsync(ScopeBy("tenant", "acme"), AllKinds, Str(string.Empty), 5, default, default))
        {
            acme.Identities.Count.ShouldBe(0);
        }

        log.Uris.Select(Uri.UnescapeDataString).ShouldNotContain(u => IsDocumentRead(u));
    }

    [TestMethod]
    public async Task A_tag_key_sharing_a_prefix_cannot_widen_another_tags_reach()
    {
        // The label token encodes key and value as separate Base64Url parts under distinct prefixes, which is
        // what keeps the mapping injective — an index widening is invisible in the results (the exact evaluation
        // discards it), so the wire is what the test observes.
        (IObservedIdentityStore store, RequestLog log) = await NewStoreAsync();
        await store.SeenAsync(Team, Str("one"), default, SecurityTagSet.FromTags([new SecurityTag("a", "bc")]), true, "test", default);
        await store.SeenAsync(Team, Str("two"), default, SecurityTagSet.FromTags([new SecurityTag("ab", "c")]), true, "test", default);

        log.Clear();
        using ObservedIdentityPage page = await store.SearchAsync(ScopeBy("a", "bc"), AllKinds, Str(string.Empty), 5, default, default);
        List<string> uris = log.Uris.Select(Uri.UnescapeDataString).ToList();

        page.Identities.Single().SubjectValueValue.ShouldBe("one");
        uris.Count(IsDocumentRead).ShouldBe(1);
    }

    [TestMethod]
    public async Task A_tag_value_carrying_table_reserved_characters_round_trips()
    {
        (IObservedIdentityStore store, _) = await NewStoreAsync();
        await store.SeenAsync(Team, Str("odd"), default, SecurityTagSet.FromTags([new SecurityTag("tenant", "a#b?c")]), true, "test", default);

        using ObservedIdentityPage page = await store.SearchAsync(ScopeBy("tenant", "a#b?c"), AllKinds, Str(string.Empty), 5, default, default);
        page.Identities.Count.ShouldBe(1);
    }

    private static bool IsLabelLookup(string unescapedUri)
        => unescapedUri.Contains("PartitionKey eq 'v~", StringComparison.Ordinal)
            || unescapedUri.Contains("PartitionKey eq 'k~", StringComparison.Ordinal);

    private static bool IsFullListing(string unescapedUri)
        => unescapedUri.Contains("PartitionKey ge ", StringComparison.Ordinal);

    private static bool IsDocumentRead(string unescapedUri)
        => unescapedUri.Contains("arazzoObservedIdentities(PartitionKey='~", StringComparison.Ordinal);

    private static SecurityTagSet Tags(string tenant) => SecurityTagSet.FromTags([new SecurityTag("tenant", tenant)]);

    private static JsonString Str(string value) => JsonString.ParseValue($"\"{value}\"");

    private static AccessContext ScopeBy(string key, string value) => AccessContext.Uniform(
        new SecurityFilter([SecurityRule.Compile($"{key} == $claim.{key}")], new Dictionary<string, IReadOnlyList<string>> { [key] = [value] }));

    private static async ValueTask<(IObservedIdentityStore Store, RequestLog Log)> NewStoreAsync()
    {
        string connectionString = container.GetConnectionString();
        var log = new RequestLog();

        var tableOptions = new TableClientOptions();
        tableOptions.AddPolicy(log, HttpPipelinePosition.PerCall);
        var tableService = new TableServiceClient(connectionString, tableOptions);

        // Provision, then clear the data, matching the conformance fixture: dropping and recreating a table is
        // eventually consistent in Azure Storage and makes the next test flaky.
        var admin = new TableServiceClient(connectionString);
        await AzureStorageObservedIdentityStore.PrepareAsync(admin);

        TableClient table = admin.GetTableClient("arazzoObservedIdentities");
        await foreach (TableEntity entity in table.QueryAsync<TableEntity>())
        {
            await table.DeleteEntityAsync(entity.PartitionKey, entity.RowKey);
        }

        return (await AzureStorageObservedIdentityStore.ConnectAsync(tableService), log);
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
