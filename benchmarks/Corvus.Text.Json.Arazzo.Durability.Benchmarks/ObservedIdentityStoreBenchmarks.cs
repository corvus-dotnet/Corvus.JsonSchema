// <copyright file="ObservedIdentityStoreBenchmarks.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using BenchmarkDotNet.Attributes;
using Corvus.Text.Json.Arazzo.Durability.Security;

namespace Corvus.Text.Json.Arazzo.Durability.Benchmarks;

/// <summary>
/// Measures the allocation floor of the <see cref="IObservedIdentityStore"/> paths (design §16.5.4), isolated
/// over the in-memory reference store (no driver / no I/O noise): a sighting upsert
/// (<see cref="IObservedIdentityStore.SeenAsync"/>) — read the existing record, union provenance, re-serialize one owned
/// document through a pooled scratch buffer (<c>byte[]</c> only at the leaf) — one keyset typeahead page
/// (<see cref="IObservedIdentityStore.SearchAsync"/>) — the prefix scan + ordered keyset over pooled documents the
/// backends push down to an index — and the grant-authoring collision probe
/// (<see cref="IObservedIdentityStore.FindIdentityConflictAsync"/>), which hands back the conflicting record as a pooled
/// document. The seam carries the grantee kind/value/label/prefix as their JSON values (no handler reification); the store
/// reifies the kind/value to its own storage key (an interned kind token) only at the leaf. None is 0 B (the upsert owns one document; the page/probe own their
/// pooled result documents); the benchmark is the regression guard that they stay small and CTJ-disciplined — no
/// <c>System.Text.Json</c> materialization, no per-row managed POCO.
/// </summary>
public class ObservedIdentityStoreBenchmarks
{
    private static readonly SecurityTagSet Identity = SecurityTagSet.FromTags(
    [
        new SecurityTag("sys:tenant", "contoso"),
        new SecurityTag("sys:sub", "alice"),
    ]);

    // The grantee kind the handler carries to the store seam as its JSON value (the store reifies it only at its key leaf).
    private static readonly ObservedIdentity.GranteeKind PersonKind = ObservedIdentity.GranteeKind.EnumValues.Person;

    // The grantee value/label/prefix the handler carries to the store seam as JSON values (no reification) — pre-parsed
    // here so the benchmark measures the store path, not the construction.
    private static readonly JsonString UpsertValue = JsonString.ParseValue("\"alice050\"");
    private static readonly JsonString UpsertLabel = JsonString.ParseValue("\"Alice 50\"");
    private static readonly JsonString SearchPrefix = JsonString.ParseValue("\"alice0\"");
    private static readonly JsonString ConflictValue = JsonString.ParseValue("\"newgrantee\"");

    private InMemoryObservedIdentityStore store = null!;

    [GlobalSetup]
    public void Setup()
    {
        this.store = new InMemoryObservedIdentityStore();

        // A realistic typeahead corpus so the prefix scan + keyset page does meaningful work.
        for (int i = 0; i < 100; i++)
        {
            this.store.SeenAsync(PersonKind, JsonString.ParseValue($"\"alice{i:D3}\""), JsonString.ParseValue($"\"Alice {i}\""), Identity, true, "accessRequest", default)
                .AsTask().GetAwaiter().GetResult();
        }
    }

    /// <summary>The steady-state sighting: re-observe an existing identity (read existing + union provenance +
    /// re-serialize). A first insert is a strict subset of this (no existing read), so this bounds the write path.</summary>
    [Benchmark]
    public void Seen_Upsert()
        => this.store.SeenAsync(PersonKind, UpsertValue, UpsertLabel, Identity, true, "administrator", default)
            .AsTask().GetAwaiter().GetResult();

    /// <summary>One keyset page of the prefix-indexed typeahead — the warm read path the grantee picker hits as the
    /// operator types. Pooled documents, ordered keyset, no STJ.</summary>
    /// <returns>The page size (prevents dead-code elimination).</returns>
    [Benchmark]
    public int Search_Page()
    {
        using ObservedIdentityPage page =
            this.store.SearchAsync(AccessContext.System, PersonKind, SearchPrefix, 10, null, default).AsTask().GetAwaiter().GetResult();
        return page.Identities.Count;
    }

    /// <summary>The grant-authoring collision probe (§16.5.4): a digest lookup against the in-memory index (O(1), no scan)
    /// that hands back the conflicting grantee's record as a pooled document (disposed here). The regression guard keeps it
    /// indexed and STJ-free — it must not degrade into a corpus scan.</summary>
    /// <returns>Whether a conflict was found (prevents dead-code elimination).</returns>
    [Benchmark]
    public bool Find_Conflict()
    {
        using var conflict =
            this.store.FindIdentityConflictAsync(PersonKind, ConflictValue, Identity, default).AsTask().GetAwaiter().GetResult();
        return conflict is not null;
    }
}
