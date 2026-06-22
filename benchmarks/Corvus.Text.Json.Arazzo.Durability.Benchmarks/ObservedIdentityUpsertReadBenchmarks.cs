// <copyright file="ObservedIdentityUpsertReadBenchmarks.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using BenchmarkDotNet.Attributes;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Corvus.Text.Json.Arazzo.Durability.Sqlite;

namespace Corvus.Text.Json.Arazzo.Durability.Benchmarks;

/// <summary>
/// Measures the <b>allocate-on-read</b> paths of <see cref="IObservedIdentityStore"/> (the read-realization row): an
/// <see cref="IObservedIdentityStore.SeenAsync"/> upsert (read the prior document → merge → re-serialize) and a
/// <see cref="IObservedIdentityStore.SearchAsync"/> keyset page (read N documents into a pooled page). The InMemory arm
/// holds documents as dictionary <c>byte[]</c> references (no per-read array — the reference floor); the SQLite arm is a
/// real embedded ADO driver (<c>Microsoft.Data.Sqlite</c>) over an in-memory database — the one relational backend whose
/// full read path is benchmarkable in-process without a container, exactly as §13.4.1 measures the credential read floor.
/// This row reads the stored document straight into a pooled document (driver <c>GetStream</c> → pooled parse) instead of
/// a GC <c>byte[]</c>, and parses the upsert merge NON-COPYING over the owned bytes — so the SQLite arms drop by the read
/// array(s) that <c>GetFieldValue&lt;byte[]&gt;</c> minted (the search page minted one per row).
/// </summary>
public class ObservedIdentityUpsertReadBenchmarks
{
    private static readonly SecurityTagSet Identity = SecurityTagSet.FromTags(
    [
        new SecurityTag("sys:tenant", "contoso"),
        new SecurityTag("sys:sub", "alice"),
    ]);

    private static readonly ObservedIdentity.GranteeKind PersonKind = ObservedIdentity.GranteeKind.EnumValues.Person;
    private static readonly JsonString UpsertValue = JsonString.ParseValue("\"alice005\"");
    private static readonly JsonString UpsertLabel = JsonString.ParseValue("\"Alice 5\"");
    private static readonly JsonString SearchPrefix = JsonString.ParseValue("\"alice0\"");

    private InMemoryObservedIdentityStore inMemory = null!;
    private SqliteObservedIdentityStore sqlite = null!;

    [GlobalSetup]
    public void Setup()
    {
        // Seed a small corpus so the upsert takes the read-merge-write path (an existing document to read) and the search
        // page returns a full keyset page (multiple documents read per call).
        this.inMemory = new InMemoryObservedIdentityStore();
        this.sqlite = SqliteObservedIdentityStore.ConnectAsync("Data Source=:memory:").AsTask().GetAwaiter().GetResult();
        for (int i = 0; i < 20; i++)
        {
            JsonString value = JsonString.ParseValue($"\"alice{i:D3}\"");
            JsonString label = JsonString.ParseValue($"\"Alice {i}\"");
            this.inMemory.SeenAsync(PersonKind, value, label, Identity, true, "accessRequest", default).AsTask().GetAwaiter().GetResult();
            this.sqlite.SeenAsync(PersonKind, value, label, Identity, true, "accessRequest", default).AsTask().GetAwaiter().GetResult();
        }
    }

    [GlobalCleanup]
    public void Cleanup() => this.sqlite.DisposeAsync().AsTask().GetAwaiter().GetResult();

    /// <summary>The upsert read floor (InMemory): the prior document is a dictionary <c>byte[]</c> reference, so the read
    /// itself allocates nothing — only the merge + re-serialize.</summary>
    [Benchmark(Baseline = true)]
    public void InMemory_Upsert()
        => this.inMemory.SeenAsync(PersonKind, UpsertValue, UpsertLabel, Identity, true, "administrator", default)
            .AsTask().GetAwaiter().GetResult();

    /// <summary>The same upsert through a REAL embedded driver (SQLite): the read-existing document plus the merge +
    /// re-serialize + write. The delta over InMemory is the driver-leaf read cost this row reduces.</summary>
    [Benchmark]
    public void Sqlite_Upsert()
        => this.sqlite.SeenAsync(PersonKind, UpsertValue, UpsertLabel, Identity, true, "administrator", default)
            .AsTask().GetAwaiter().GetResult();

    /// <summary>One keyset typeahead page over the in-memory reference (the projection-read floor): pooled documents,
    /// ordered keyset, no per-read driver array.</summary>
    /// <returns>The page size (prevents dead-code elimination).</returns>
    [Benchmark]
    public int InMemory_Search()
    {
        using ObservedIdentityPage page =
            this.inMemory.SearchAsync(AccessContext.System, PersonKind, SearchPrefix, 10, default, default).AsTask().GetAwaiter().GetResult();
        return page.Identities.Count;
    }

    /// <summary>The same keyset page through a REAL embedded driver (SQLite): N documents read into the pooled page. This
    /// row reads each row straight into its pooled document (GetStream → parse) — where the old path minted one GC
    /// <c>byte[]</c> per row and then copied it again into the pooled document.</summary>
    /// <returns>The page size (prevents dead-code elimination).</returns>
    [Benchmark]
    public int Sqlite_Search()
    {
        using ObservedIdentityPage page =
            this.sqlite.SearchAsync(AccessContext.System, PersonKind, SearchPrefix, 10, default, default).AsTask().GetAwaiter().GetResult();
        return page.Identities.Count;
    }
}
