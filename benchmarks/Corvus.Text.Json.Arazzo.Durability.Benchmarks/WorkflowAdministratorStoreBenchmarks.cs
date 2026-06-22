// <copyright file="WorkflowAdministratorStoreBenchmarks.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using BenchmarkDotNet.Attributes;
using Corvus.Runtime.InteropServices;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Security;

namespace Corvus.Text.Json.Arazzo.Durability.Benchmarks;

/// <summary>
/// Measures the <see cref="IWorkflowAdministratorStore"/> write seam (design §15) as the backends realize it: the
/// administration record is serialized once and persisted/returned. The <c>ToArray</c> arms are the <b>before</b>/byte[]-leaf
/// shape (one owned GC document array, as Mongo/NATS/Azure/Sqlite + the InMemory dict realize it); the <c>Doc</c> arms are
/// the memory/stream shape this row introduced (SqlServer/Postgres/MySql/Redis) — serialized once into the pooled buffer the
/// <em>returned</em> document owns and bound via <c>JsonMarshal</c>, so the GC document array is dropped. The update arms
/// additionally carry the existing record as the already-parsed model (one non-copying parse at the leaf, replacing the old
/// EtagOf + SerializeUpdated double-parse).
/// </summary>
[MemoryDiagnoser]
public class WorkflowAdministratorStoreBenchmarks
{
    private static readonly IReadOnlyList<SecurityTagSet> Admins =
    [
        SecurityTagSet.FromTags([new SecurityTag("sys:tenant", "contoso"), new SecurityTag("sys:sub", "alice")]),
        SecurityTagSet.FromTags([new SecurityTag("sys:tenant", "contoso"), new SecurityTag("sys:sub", "bob")]),
    ];

    private static readonly DateTimeOffset At = DateTimeOffset.UnixEpoch;
    private static readonly WorkflowEtag Etag = new("etag-bench");

    // A pre-parsed existing record (owned, disposed in cleanup) so the update arms measure the merge serialize, not the seed.
    private ParsedJsonDocument<WorkflowAdministrators> existing = null!;

    [GlobalSetup]
    public void Setup()
    {
        byte[] seed = WorkflowAdministratorsSerialization.SerializeNew("nightly-reconcile", Admins, "admin", At, Etag);
        this.existing = PersistedJson.ToPooledDocument<WorkflowAdministrators>(seed);
    }

    [GlobalCleanup]
    public void Cleanup() => this.existing.Dispose();

    /// <summary>The create write as the byte[]-leaf backends realize it: one owned GC document array.</summary>
    /// <returns>The document length (prevents dead-code elimination).</returns>
    [Benchmark(Baseline = true)]
    public int Serialize_New_ToArray()
        => WorkflowAdministratorsSerialization.SerializeNew("nightly-reconcile", Admins, "admin", At, Etag).Length;

    /// <summary>The same create write as the memory/stream backends realize it: the owning pooled document the store returns
    /// (buffer pooled, no GC array), bound via <c>JsonMarshal</c>.</summary>
    /// <returns>The document length (prevents dead-code elimination).</returns>
    [Benchmark]
    public int Serialize_New_Doc()
    {
        using ParsedJsonDocument<WorkflowAdministrators> doc = WorkflowAdministratorsSerialization.SerializeNewDoc("nightly-reconcile", Admins, "admin", At, Etag);
        return JsonMarshal.GetRawUtf8Value(doc.RootElement).Memory.Length;
    }

    /// <summary>The carried-forward update write as the byte[]-leaf backends realize it: one owned GC document array (the
    /// existing record already parsed once, non-copying, at the leaf).</summary>
    /// <returns>The document length (prevents dead-code elimination).</returns>
    [Benchmark]
    public int Serialize_Updated_ToArray()
        => WorkflowAdministratorsSerialization.SerializeUpdated(this.existing.RootElement, Admins, "admin", At, Etag).Length;

    /// <summary>The same update write as the memory/stream backends realize it: the owning pooled document the store returns
    /// (buffer pooled, no GC array).</summary>
    /// <returns>The document length (prevents dead-code elimination).</returns>
    [Benchmark]
    public int Serialize_Updated_Doc()
    {
        using ParsedJsonDocument<WorkflowAdministrators> doc = WorkflowAdministratorsSerialization.SerializeUpdatedDoc(this.existing.RootElement, Admins, "admin", At, Etag);
        return JsonMarshal.GetRawUtf8Value(doc.RootElement).Memory.Length;
    }
}
