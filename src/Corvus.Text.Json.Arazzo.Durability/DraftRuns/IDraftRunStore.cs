// <copyright file="IDraftRunStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// The sibling store carrying a §18 draft run's captured draft: the packed document + sources (a
/// <see cref="WorkflowPackage"/> container, exactly the catalog's artifact framing) and the audited
/// <see cref="DraftRun"/> capture record, keyed by the run id. The control plane writes the capture once at
/// start (before enqueueing the Pending run); the claiming runner reads it to compile and execute — the store is
/// the only channel between them (design §2), and the draft never enters the catalog.
/// </summary>
/// <remarks>
/// This mirrors the catalog/run split for published versions: the run record carries run <em>state</em> (and is
/// re-serialised on every checkpoint), while the immutable executable <em>artifact</em> lives beside it, written
/// once and cached by content hash on the runner. Backends persist the record verbatim as its JSON document and
/// the package as an opaque blob, like a catalog version's package.
/// </remarks>
public interface IDraftRunStore
{
    /// <summary>
    /// Persists (or replaces) a draft run's capture: the audit record and the packed document + sources.
    /// </summary>
    /// <param name="id">The run id the capture belongs to.</param>
    /// <param name="record">The audited capture record.</param>
    /// <param name="package">The packed document + sources (<see cref="WorkflowPackage"/> bytes).</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that completes when the capture is durable.</returns>
    ValueTask PutAsync(WorkflowRunId id, DraftRun record, ReadOnlyMemory<byte> package, CancellationToken cancellationToken);

    /// <summary>Reads a draft run's capture record.</summary>
    /// <param name="id">The run id.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The record as a pooled document the caller must dispose, or <see langword="null"/> when no capture exists.</returns>
    ValueTask<ParsedJsonDocument<DraftRun>?> GetAsync(WorkflowRunId id, CancellationToken cancellationToken);

    /// <summary>Reads a draft run's captured package (the packed document + sources).</summary>
    /// <param name="id">The run id.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The package bytes, or <see langword="null"/> when no capture exists.</returns>
    ValueTask<ReadOnlyMemory<byte>?> GetPackageAsync(WorkflowRunId id, CancellationToken cancellationToken);

    /// <summary>Deletes a draft run's capture (record and package).</summary>
    /// <param name="id">The run id.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns><see langword="true"/> if a capture existed and was deleted.</returns>
    ValueTask<bool> DeleteAsync(WorkflowRunId id, CancellationToken cancellationToken);
}