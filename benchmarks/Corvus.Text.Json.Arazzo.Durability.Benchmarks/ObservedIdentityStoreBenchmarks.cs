// <copyright file="ObservedIdentityStoreBenchmarks.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using BenchmarkDotNet.Attributes;
using Corvus.Text.Json.Arazzo.Durability.Security;

namespace Corvus.Text.Json.Arazzo.Durability.Benchmarks;

/// <summary>
/// Measures the allocation floor of the <see cref="IObservedIdentityStore"/> paths (design §16.5.4), isolated
/// over the in-memory reference store (no driver / no I/O noise): a sighting upsert
/// (<see cref="IObservedIdentityStore.SeenAsync"/>) — read the existing record, union provenance, re-serialize one owned
/// document through a pooled scratch buffer (<c>byte[]</c> only at the leaf) — and one keyset typeahead page
/// (<see cref="IObservedIdentityStore.SearchAsync"/>) — the prefix scan + ordered keyset over pooled documents the
/// backends push down to an index. Neither is 0 B (the upsert owns one document; the page owns its result documents); the
/// benchmark is the regression guard that they stay small and CTJ-disciplined — no <c>System.Text.Json</c>
/// materialization, no per-row managed POCO.
/// </summary>
public class ObservedIdentityStoreBenchmarks
{
    private static readonly SecurityTagSet Identity = SecurityTagSet.FromTags(
    [
        new SecurityTag("sys:tenant", "contoso"),
        new SecurityTag("sys:sub", "alice"),
    ]);

    // The unescaped UTF-8 value/label/prefix bytes the handler already holds (GetUtf8String().TakeOwnership) and threads
    // to the store seam — pre-encoded here so the benchmark measures the store path, not the transcode.
    private static readonly ReadOnlyMemory<byte> UpsertValue = "alice050"u8.ToArray();
    private static readonly ReadOnlyMemory<byte> UpsertLabel = "Alice 50"u8.ToArray();
    private static readonly ReadOnlyMemory<byte> SearchPrefix = "alice0"u8.ToArray();
    private static readonly ReadOnlyMemory<byte> ConflictValue = "newgrantee"u8.ToArray();

    private InMemoryObservedIdentityStore store = null!;

    [GlobalSetup]
    public void Setup()
    {
        this.store = new InMemoryObservedIdentityStore();

        // A realistic typeahead corpus so the prefix scan + keyset page does meaningful work.
        for (int i = 0; i < 100; i++)
        {
            this.store.SeenAsync(GranteeKind.Person, Encoding.UTF8.GetBytes($"alice{i:D3}"), Encoding.UTF8.GetBytes($"Alice {i}"), Identity, true, "accessRequest", default)
                .AsTask().GetAwaiter().GetResult();
        }
    }

    /// <summary>The steady-state sighting: re-observe an existing identity (read existing + union provenance +
    /// re-serialize). A first insert is a strict subset of this (no existing read), so this bounds the write path.</summary>
    [Benchmark]
    public void Seen_Upsert()
        => this.store.SeenAsync(GranteeKind.Person, UpsertValue, UpsertLabel, Identity, true, "administrator", default)
            .AsTask().GetAwaiter().GetResult();

    /// <summary>One keyset page of the prefix-indexed typeahead — the warm read path the grantee picker hits as the
    /// operator types. Pooled documents, ordered keyset, no STJ.</summary>
    /// <returns>The page size (prevents dead-code elimination).</returns>
    [Benchmark]
    public int Search_Page()
    {
        using ObservedIdentityPage page =
            this.store.SearchAsync(AccessContext.System, GranteeKind.Person, SearchPrefix, 10, null, default).AsTask().GetAwaiter().GetResult();
        return page.Identities.Count;
    }

    /// <summary>The grant-authoring collision probe (§16.5.4): a digest lookup against the in-memory index (O(1), no scan)
    /// plus materialising the conflicting grantee's label. Low-frequency (an admin authoring a grant, not a warm hot
    /// path), but the regression guard keeps it indexed and STJ-free — it must not degrade into a corpus scan.</summary>
    /// <returns>Whether a conflict was found (prevents dead-code elimination).</returns>
    [Benchmark]
    public bool Find_Conflict()
        => this.store.FindIdentityConflictAsync(GranteeKind.Person, ConflictValue, Identity, default).AsTask().GetAwaiter().GetResult() is not null;
}