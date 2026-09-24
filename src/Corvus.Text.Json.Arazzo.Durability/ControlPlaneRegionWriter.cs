// <copyright file="ControlPlaneRegionWriter.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// The one write the control plane makes to a run's row (ADR 0065 decision 7): a new control-plane region over the
/// runner's bytes as they are stored, with the index projected from the join. If the row moves between the load and
/// the write (a runner save landed meanwhile), the decision is re-applied over what is there now, so a control-plane
/// write never rewrites a runner's newer bytes and a runner's save never loses a control-plane decision.
/// </summary>
internal static class ControlPlaneRegionWriter
{
    private const int MergeAttempts = 3;

    /// <summary>Writes the region <paramref name="decide"/> returns over the stored row, re-applying it if the row moves.</summary>
    /// <param name="store">The store.</param>
    /// <param name="address">The run's address.</param>
    /// <param name="decide">Given the stored row's projection and its current record, the record to write, or <see langword="null"/> to leave the row as it is.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The outcome, with the projection of the row as it was read on the last attempt.</returns>
    public static async ValueTask<ControlPlaneWrite> WriteAsync(IWorkflowCheckpointStore store, WorkflowRunAddress address, Func<CheckpointProjection, ControlPlaneRecord, ControlPlaneRecord?> decide, CancellationToken cancellationToken)
    {
        for (int attempt = 1; ; attempt++)
        {
            WorkflowCheckpoint? stored = await store.LoadAsync(address, cancellationToken).ConfigureAwait(false);
            if (stored is not { } row)
            {
                return new ControlPlaneWrite(ControlPlaneWriteOutcome.Missing, default, WorkflowEtag.None);
            }

            if (!WorkflowCheckpointSerializer.TryProject(row.Row, out CheckpointProjection projection))
            {
                return new ControlPlaneWrite(ControlPlaneWriteOutcome.Malformed, default, row.Etag);
            }

            if (decide(projection, ControlPlaneRecord.Parse(projection.ControlPlaneRegion)) is not { } decided)
            {
                return new ControlPlaneWrite(ControlPlaneWriteOutcome.Unchanged, projection, row.Etag);
            }

            byte[] rewritten = CheckpointRow.WithControlPlaneRegion(row.Row.Span, decided.ToUtf8());
            try
            {
                WorkflowEtag etag = await store.SaveAsync(address, rewritten, WorkflowCheckpointSerializer.ProjectIndex(rewritten), row.Etag, cancellationToken).ConfigureAwait(false);
                return new ControlPlaneWrite(ControlPlaneWriteOutcome.Written, projection, etag);
            }
            catch (WorkflowConflictException) when (attempt < MergeAttempts)
            {
                // A runner save landed between the load and the write; the decision is re-applied over its bytes.
            }
            catch (WorkflowConflictException)
            {
                return new ControlPlaneWrite(ControlPlaneWriteOutcome.Conflict, projection, row.Etag);
            }
        }
    }
}

/// <summary>The result of a control-plane region write.</summary>
/// <param name="Outcome">What happened.</param>
/// <param name="Projection">The projection of the row as last read (before the write), or <see langword="default"/> when there was no row to read.</param>
/// <param name="Etag">The row's etag after a write, or as read otherwise.</param>
internal readonly record struct ControlPlaneWrite(ControlPlaneWriteOutcome Outcome, CheckpointProjection Projection, WorkflowEtag Etag);

/// <summary>The outcomes of a control-plane region write.</summary>
internal enum ControlPlaneWriteOutcome
{
    /// <summary>The region was written over the runner's stored bytes.</summary>
    Written,

    /// <summary>The decision left the row as it was.</summary>
    Unchanged,

    /// <summary>There is no row at the address.</summary>
    Missing,

    /// <summary>The stored row does not project.</summary>
    Malformed,

    /// <summary>The row kept moving under the write.</summary>
    Conflict,
}