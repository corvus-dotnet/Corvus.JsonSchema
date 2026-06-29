// <copyright file="EnvironmentAdministratorReverseIndexBenchmarks.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using BenchmarkDotNet.Attributes;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Security;

namespace Corvus.Text.Json.Arazzo.Durability.Benchmarks;

/// <summary>
/// Measures the environment-administrator <b>reverse index</b> read (design §7.8 approver inbox) over the in-memory
/// reference store: <see cref="IEnvironmentAdministratorStore.ListAdministeredAsync"/> answers "which environments does
/// this resolved identity administer?" — the per-request query the availability-request inbox runs to scope an
/// approver's queue. It is the read counterpart of the index <see cref="IEnvironmentAdministratorStore.PutAsync"/>
/// maintains (retract-then-index on every administrator-set change).
/// <para>
/// The page owns no pooled documents — its <see cref="EnvironmentAdministeredPage.EnvironmentNames"/> are detached
/// strings and only the (optional) continuation token rides in a pooled buffer — so this records the keyset page's own
/// allocation as the baseline-to-beat for the backend fan-out. The store is pre-seeded so the measured region is the
/// reverse-index seek + page build, not the index maintenance.
/// </para>
/// </summary>
[MemoryDiagnoser]
public class EnvironmentAdministratorReverseIndexBenchmarks
{
    private const string Actor = "bench";
    private const int SeededCount = 50;
    private const int PageSize = 20;

    // The workspace outlives Setup (it holds the built identity), so it is the unrented form; disposed in cleanup.
    private JsonWorkspace workspace = null!;
    private InMemoryEnvironmentAdministratorStore store = null!;

    // The digest of the identity that administers every seeded environment — the reverse-index key the page is keyed on.
    private string adminDigest = null!;

    [GlobalSetup]
    public void Setup()
    {
        this.workspace = JsonWorkspace.CreateUnrented();

        // One resolved identity (tags only, the persisted form) that administers every seeded environment, plus a
        // per-environment co-administrator so each set is realistic (never a single-holder degenerate index).
        SecurityTagSet sharedTags = SecurityTagSet.FromTags([new SecurityTag("sys:tenant", "contoso"), new SecurityTag("sys:sub", "platform-sre")]);
        EnvironmentAdministrators.AdministratorIdentity shared =
            EnvironmentAdministrators.BuildIdentity(this.workspace, sharedTags, default, hasKind: false, default, hasLabel: false);
        this.adminDigest = EnvironmentAdministeredPaging.DistinctDigests([shared])[0];

        this.store = new InMemoryEnvironmentAdministratorStore();
        for (int i = 0; i < SeededCount; i++)
        {
            SecurityTagSet ownerTags = SecurityTagSet.FromTags([new SecurityTag("sys:tenant", "contoso"), new SecurityTag("sys:sub", $"owner-{i:D3}")]);
            EnvironmentAdministrators.AdministratorIdentity owner =
                EnvironmentAdministrators.BuildIdentity(this.workspace, ownerTags, default, hasKind: false, default, hasLabel: false);
            using ParsedJsonDocument<EnvironmentAdministrators> put =
                this.store.PutAsync($"env-{i:D3}", [shared, owner], WorkflowEtag.None, Actor, default).AsTask().GetAwaiter().GetResult();
        }
    }

    [GlobalCleanup]
    public void Cleanup() => this.workspace.Dispose();

    /// <summary>The reverse-index read: one keyset page of the environments this identity administers (dispose the page as
    /// the inbox handler does).</summary>
    [Benchmark]
    public void List_AdministeredPage()
    {
        using EnvironmentAdministeredPage page =
            this.store.ListAdministeredAsync(this.adminDigest, PageSize, default, default).AsTask().GetAwaiter().GetResult();
    }
}