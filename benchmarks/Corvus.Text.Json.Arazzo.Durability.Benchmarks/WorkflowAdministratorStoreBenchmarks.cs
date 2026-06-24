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
    private static readonly IReadOnlyList<SecurityTagSet> AdminTags =
    [
        SecurityTagSet.FromTags([new SecurityTag("sys:tenant", "contoso"), new SecurityTag("sys:sub", "alice")]),
        SecurityTagSet.FromTags([new SecurityTag("sys:tenant", "contoso"), new SecurityTag("sys:sub", "bob")]),
    ];

    private static readonly DateTimeOffset At = DateTimeOffset.UnixEpoch;
    private static readonly WorkflowEtag Etag = new("etag-bench");

    // The resolved administrator identities (tags only, the persisted form), materialized once in a held workspace. The
    // workspace is the unrented form because it outlives Setup; it is disposed in cleanup.
    private JsonWorkspace workspace = null!;
    private IReadOnlyList<WorkflowAdministrators.AdministratorIdentity> admins = null!;

    // A pre-parsed existing record (owned, disposed in cleanup) so the update arms measure the merge serialize, not the seed.
    private ParsedJsonDocument<WorkflowAdministrators> existing = null!;

    [GlobalSetup]
    public void Setup()
    {
        this.workspace = JsonWorkspace.CreateUnrented();
        this.admins = [.. AdminTags.Select(t => WorkflowAdministrators.BuildIdentity(this.workspace, t, default, hasKind: false, default, hasLabel: false))];
        byte[] seed = WorkflowAdministratorsSerialization.SerializeNew("nightly-reconcile", this.admins, "admin", At, Etag);
        this.existing = PersistedJson.ToPooledDocument<WorkflowAdministrators>(seed);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        this.existing.Dispose();
        this.workspace.Dispose();
    }

    /// <summary>The create write as the byte[]-leaf backends realize it: one owned GC document array.</summary>
    /// <returns>The document length (prevents dead-code elimination).</returns>
    [Benchmark(Baseline = true)]
    public int Serialize_New_ToArray()
        => WorkflowAdministratorsSerialization.SerializeNew("nightly-reconcile", this.admins, "admin", At, Etag).Length;

    /// <summary>The same create write as the memory/stream backends realize it: the owning pooled document the store returns
    /// (buffer pooled, no GC array), bound via <c>JsonMarshal</c>.</summary>
    /// <returns>The document length (prevents dead-code elimination).</returns>
    [Benchmark]
    public int Serialize_New_Doc()
    {
        using ParsedJsonDocument<WorkflowAdministrators> doc = WorkflowAdministratorsSerialization.SerializeNewDoc("nightly-reconcile", this.admins, "admin", At, Etag);
        return JsonMarshal.GetRawUtf8Value(doc.RootElement).Memory.Length;
    }

    /// <summary>The carried-forward update write as the byte[]-leaf backends realize it: one owned GC document array (the
    /// existing record already parsed once, non-copying, at the leaf).</summary>
    /// <returns>The document length (prevents dead-code elimination).</returns>
    [Benchmark]
    public int Serialize_Updated_ToArray()
        => WorkflowAdministratorsSerialization.SerializeUpdated(this.existing.RootElement, this.admins, "admin", At, Etag).Length;

    /// <summary>The same update write as the memory/stream backends realize it: the owning pooled document the store returns
    /// (buffer pooled, no GC array).</summary>
    /// <returns>The document length (prevents dead-code elimination).</returns>
    [Benchmark]
    public int Serialize_Updated_Doc()
    {
        using ParsedJsonDocument<WorkflowAdministrators> doc = WorkflowAdministratorsSerialization.SerializeUpdatedDoc(this.existing.RootElement, this.admins, "admin", At, Etag);
        return JsonMarshal.GetRawUtf8Value(doc.RootElement).Memory.Length;
    }
}
