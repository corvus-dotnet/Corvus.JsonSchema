// <copyright file="IDraftRunTraceStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// The sibling store carrying a §18 debug (<c>$draft</c>) run's latest assembled metadata trace, keyed by the run
/// id: the runner writes the trace after each advance, and a control plane in a <em>different</em> process reads it
/// to answer <c>get-debug-run</c>. It is the durable, cross-process replacement for the in-process, ephemeral
/// trace cache <c>InProcessDraftRunner</c> holds — with it, the process that advanced the run no longer has to be
/// the process that answers the read (design §2's process split, workflow-designer design §18).
/// </summary>
/// <remarks>
/// This mirrors the draft-run store's package blob exactly. The trace is opaque UTF-8 JSON — a
/// <c>SimulationTrace</c>-shaped metadata document with no request or response bodies (the ratified §18 body
/// posture) — that the store persists and hands back verbatim, never parsing it, just as
/// <see cref="IDraftRunStore"/> treats its packed <c>{document, sources}</c> artifact. There is exactly one trace
/// per run, overwritten on each advance; it is purged with the run (the §18 ruling: traces are purged with the
/// run), so the store deletes rather than versions. Backends persist it as an opaque blob keyed by run id.
/// </remarks>
public interface IDraftRunTraceStore
{
    /// <summary>
    /// Persists a debug run's latest assembled metadata trace, overwriting any prior trace for the run (the runner
    /// calls this after each advance).
    /// </summary>
    /// <param name="id">The run id the trace belongs to.</param>
    /// <param name="traceUtf8">The assembled metadata trace as opaque UTF-8 JSON (a <c>SimulationTrace</c>-shaped document).</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that completes when the trace is durable.</returns>
    ValueTask PutAsync(WorkflowRunId id, ReadOnlyMemory<byte> traceUtf8, CancellationToken cancellationToken);

    /// <summary>Reads a debug run's latest assembled metadata trace.</summary>
    /// <param name="id">The run id.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The trace bytes, or <see langword="null"/> when no trace exists for the run.</returns>
    ValueTask<ReadOnlyMemory<byte>?> GetAsync(WorkflowRunId id, CancellationToken cancellationToken);

    /// <summary>Deletes a debug run's trace (purged with the run — the §18 ruling).</summary>
    /// <param name="id">The run id.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns><see langword="true"/> if a trace existed and was deleted.</returns>
    ValueTask<bool> DeleteAsync(WorkflowRunId id, CancellationToken cancellationToken);
}