// <copyright file="DraftRunStoreBenchmarks.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;
using BenchmarkDotNet.Attributes;
using Corvus.Text.Json.Arazzo.Durability;

namespace Corvus.Text.Json.Arazzo.Durability.Benchmarks;

/// <summary>
/// The allocation floor of the §18 draft-run store seam (<see cref="IDraftRunStore"/>), isolated over the in-memory
/// reference store (no driver / no I/O noise): the capture PUT (serialize the audit record + own the packed draft),
/// the record GET (parse into a pooled document the caller disposes), and the package GET (hand back the owned blob).
/// </summary>
/// <remarks>
/// The two <c>Package_Bind_*</c> arms are the measured contract the backend fan-out is held to. The package is opaque
/// bytes arriving as a <see cref="ReadOnlyMemory{T}"/>; the network drivers (SqlServer/Postgres/MySql/Redis) must
/// stream it through a pooled <see cref="ReadOnlyMemoryStream"/> as their BLOB parameter — never mint a per-put GC
/// array with <c>ToArray()</c> — exactly as <see cref="WorkflowStateStoreBenchmarks"/> contrasts the checkpoint bind
/// (the #803 row that took those backends off the per-save array). SQLite legitimately binds <c>byte[]</c> parameters,
/// so <c>SqliteDraftRunStore</c>'s <c>ToArray</c> matches its sibling <c>SqliteWorkflowStateStore</c>; this arm is the
/// regression guard that the stream backends do not reintroduce the copy.
/// </remarks>
[MemoryDiagnoser]
public class DraftRunStoreBenchmarks
{
    private static readonly WorkflowRunId Id = new("draftrun00000000000000000000000000");

    // A representative packed { workflow, sources } blob (~8 KB — a small draft with one source), the opaque artifact
    // the store persists and the runner later compiles. Only its size matters to the bind/own allocation.
    private static readonly byte[] Package = CreatePackage();

    private static readonly byte[] RecordUtf8 = CreateRecordUtf8();

    private InMemoryDraftRunStore store = null!;
    private DraftRun record;

    [GlobalSetup]
    public void Setup()
    {
        // A detached record (one owned copy, workspace-independent) held for the run's lifetime, so the PUT arm measures
        // the store's serialize + own, not a per-iteration parse.
        this.record = DraftRun.FromJson(RecordUtf8.AsMemory());

        this.store = new InMemoryDraftRunStore();
        this.store.PutAsync(Id, this.record, Package.AsMemory(), default).AsTask().GetAwaiter().GetResult();
    }

    /// <summary>The capture PUT: serialize the audit record to its JSON document and own the packed draft. The
    /// reference floor the durable backends are measured against.</summary>
    /// <returns>Zero (prevents dead-code elimination).</returns>
    [Benchmark]
    public int Put()
    {
        this.store.PutAsync(Id, this.record, Package.AsMemory(), default).AsTask().GetAwaiter().GetResult();
        return 0;
    }

    /// <summary>The record GET: parse the persisted document into a pooled <see cref="ParsedJsonDocument{T}"/> the caller
    /// disposes. The warm read the runner and the (later) debug-run view hit.</summary>
    /// <returns>The run-id length (prevents dead-code elimination).</returns>
    [Benchmark]
    public int Get()
    {
        using ParsedJsonDocument<DraftRun>? doc = this.store.GetAsync(Id, default).AsTask().GetAwaiter().GetResult();
        return doc!.RootElement.RunIdValue.Length;
    }

    /// <summary>The package GET: hand back the owned blob the runner compiles. No parse, no copy on the reference store.</summary>
    /// <returns>The package length (prevents dead-code elimination).</returns>
    [Benchmark]
    public int GetPackage()
    {
        ReadOnlyMemory<byte>? package = this.store.GetPackageAsync(Id, default).AsTask().GetAwaiter().GetResult();
        return package!.Value.Length;
    }

    /// <summary>The package blob bound as a per-put GC array — the arm the network drivers must NOT use. Present so the
    /// stream arm's win is measured, not asserted.</summary>
    /// <returns>The blob length (prevents dead-code elimination).</returns>
    [Benchmark]
    public int Package_Bind_ToArray()
        => Package.AsMemory().ToArray().Length;

    /// <summary>The package blob bound as the network drivers realize it: a pooled <see cref="ReadOnlyMemoryStream"/> the
    /// driver streams as its BLOB parameter — no per-put GC array. The bar the fan-out is held to.</summary>
    /// <returns>The blob length (prevents dead-code elimination).</returns>
    [Benchmark]
    public int Package_Bind_StreamRent()
    {
        using ReadOnlyMemoryStream stream = ReadOnlyMemoryStream.Rent(Package.AsMemory());
        return (int)stream.Length;
    }

    private static byte[] CreatePackage()
    {
        var bytes = new byte[8192];
        for (int i = 0; i < bytes.Length; i++)
        {
            bytes[i] = (byte)('a' + (i % 26));
        }

        return bytes;
    }

    private static byte[] CreateRecordUtf8()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<DraftRun.Mutable> builder = DraftRun.CreateBuilder(
            workspace,
            runId: "draftrun00000000000000000000000000",
            workingCopyId: "wc-orders-9f2",
            workflowId: "process-order",
            documentEtag: "\"etag-1\"",
            environment: "development",
            startedBy: "user:alice",
            startedAt: DateTimeOffset.UnixEpoch,
            contentHash: "sha256-0000000000000000000000000000000000000000000000000000000000000000");
        return PersistedJson.ToArray(builder.RootElement, static (Utf8JsonWriter writer, in DraftRun r) => r.WriteTo(writer));
    }
}
