// <copyright file="DraftRunTraceStoreBenchmarks.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using BenchmarkDotNet.Attributes;
using Corvus.Text.Json.Arazzo.Durability;

namespace Corvus.Text.Json.Arazzo.Durability.Benchmarks;

/// <summary>
/// The allocation floor of the §18 draft-run-trace store seam (<see cref="IDraftRunTraceStore"/>), isolated over
/// the in-memory reference store (no driver / no I/O noise): the trace PUT (own the assembled metadata trace the
/// runner writes after each advance) and the trace GET (hand back the owned blob the control plane's
/// <c>get-debug-run</c> reads).
/// </summary>
/// <remarks>
/// The two <c>Trace_Bind_*</c> arms are the measured contract the backend fan-out is held to. The trace is opaque
/// UTF-8 JSON arriving as a <see cref="ReadOnlyMemory{T}"/>; the network drivers (SqlServer/Postgres/MySql/Redis)
/// must stream it through a pooled <see cref="ReadOnlyMemoryStream"/> as their BLOB parameter — never mint a
/// per-put GC array with <c>ToArray()</c> — exactly as <see cref="DraftRunStoreBenchmarks"/> contrasts the package
/// bind. SQLite legitimately binds <c>byte[]</c> parameters, so <c>SqliteDraftRunTraceStore</c>'s <c>ToArray</c>
/// matches its sibling <c>SqliteDraftRunStore</c>; this arm is the regression guard that the stream backends do not
/// reintroduce the copy.
/// </remarks>
[MemoryDiagnoser]
public class DraftRunTraceStoreBenchmarks
{
    private static readonly WorkflowRunId Id = new("draftrun00000000000000000000000000");

    // A representative assembled metadata trace (~3 KB — an outcome plus a handful of bodies-omitted step records), the
    // opaque UTF-8 JSON the runner writes after each advance and the control plane later reads. Only its size matters to
    // the bind/own allocation.
    private static readonly byte[] Trace = CreateTrace();

    private InMemoryDraftRunTraceStore store = null!;

    [GlobalSetup]
    public void Setup()
    {
        this.store = new InMemoryDraftRunTraceStore();
        this.store.PutAsync(Id, Trace.AsMemory(), default).AsTask().GetAwaiter().GetResult();
    }

    /// <summary>The trace PUT: own the assembled metadata trace the runner writes after each advance. The reference
    /// floor the durable backends are measured against.</summary>
    /// <returns>Zero (prevents dead-code elimination).</returns>
    [Benchmark]
    public int Put()
    {
        this.store.PutAsync(Id, Trace.AsMemory(), default).AsTask().GetAwaiter().GetResult();
        return 0;
    }

    /// <summary>The trace GET: hand back the owned blob the control plane's <c>get-debug-run</c> reads. No parse, no
    /// copy on the reference store.</summary>
    /// <returns>The trace length (prevents dead-code elimination).</returns>
    [Benchmark]
    public int Get()
    {
        ReadOnlyMemory<byte>? trace = this.store.GetAsync(Id, default).AsTask().GetAwaiter().GetResult();
        return trace!.Value.Length;
    }

    /// <summary>The trace blob bound as a per-put GC array — the arm the network drivers must NOT use. Present so the
    /// stream arm's win is measured, not asserted.</summary>
    /// <returns>The blob length (prevents dead-code elimination).</returns>
    [Benchmark]
    public int Trace_Bind_ToArray()
        => Trace.AsMemory().ToArray().Length;

    /// <summary>The trace blob bound as the network drivers realize it: a pooled <see cref="ReadOnlyMemoryStream"/> the
    /// driver streams as its BLOB parameter — no per-put GC array. The bar the fan-out is held to.</summary>
    /// <returns>The blob length (prevents dead-code elimination).</returns>
    [Benchmark]
    public int Trace_Bind_StreamRent()
    {
        using ReadOnlyMemoryStream stream = ReadOnlyMemoryStream.Rent(Trace.AsMemory());
        return (int)stream.Length;
    }

    private static byte[] CreateTrace()
    {
        var bytes = new byte[3072];
        for (int i = 0; i < bytes.Length; i++)
        {
            bytes[i] = (byte)('a' + (i % 26));
        }

        return bytes;
    }
}