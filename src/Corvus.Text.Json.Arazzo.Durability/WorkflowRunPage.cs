// <copyright file="WorkflowRunPage.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// A page of runs matching a <see cref="WorkflowQuery"/> (plan §11): the matching runs ordered by ascending run id, plus
/// an opaque <see cref="NextPageToken"/> to fetch the next page (empty when this is the last page). The token (when
/// present) is held in a pooled buffer — <see cref="Dispose"/> the page once read to return it to the pool.
/// </summary>
/// <remarks>
/// The token is built bytes-native: <see cref="Create(IReadOnlyList{WorkflowRunListing}, string)"/> Base64URL-encodes the
/// last run id straight into a pooled buffer (no token string), exposed as <see cref="NextPageToken"/> UTF-8 the handler
/// writes verbatim into the response. The buffer outlives the synchronous response build and is returned on
/// <see cref="Dispose"/>. This is a class, not a record struct: it owns a rented buffer, which a value-copy of a struct
/// would double-return on dispose.
/// </remarks>
public sealed class WorkflowRunPage : IDisposable
{
    private byte[]? rentedToken;

    private WorkflowRunPage(IReadOnlyList<WorkflowRunListing> runs, ReadOnlyMemory<byte> nextPageToken, byte[]? rentedToken)
    {
        this.Runs = runs;
        this.NextPageToken = nextPageToken;
        this.rentedToken = rentedToken;
    }

    /// <summary>Gets the matching runs (at most <see cref="WorkflowQuery.Limit"/>), ordered by ascending run id.</summary>
    public IReadOnlyList<WorkflowRunListing> Runs { get; }

    /// <summary>Gets the opaque continuation token (UTF-8) to fetch the next page, or empty if this is the last page.</summary>
    public ReadOnlyMemory<byte> NextPageToken { get; }

    /// <summary>Creates a last page (no continuation token).</summary>
    /// <param name="runs">The matching runs, ordered by ascending run id.</param>
    /// <returns>The page.</returns>
    public static WorkflowRunPage Create(IReadOnlyList<WorkflowRunListing> runs)
        => new(runs, default, null);

    /// <summary>Creates a page with a continuation token, encoding the last run id straight into a pooled buffer (no
    /// intermediate token string).</summary>
    /// <param name="runs">The matching runs, ordered by ascending run id.</param>
    /// <param name="lastRunId">The last run id of the page (the keyset cursor the next page resumes after).</param>
    /// <returns>The page, owning the pooled token buffer.</returns>
    public static WorkflowRunPage Create(IReadOnlyList<WorkflowRunListing> runs, string lastRunId)
    {
        int maxLength = WorkflowContinuationToken.GetMaxEncodedLength(lastRunId);
        byte[] buffer = ArrayPool<byte>.Shared.Rent(maxLength);
        try
        {
            int written = WorkflowContinuationToken.EncodeToUtf8(lastRunId, buffer);
            return new WorkflowRunPage(runs, buffer.AsMemory(0, written), buffer);
        }
        catch
        {
            ArrayPool<byte>.Shared.Return(buffer);
            throw;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (this.rentedToken is { } buffer)
        {
            this.rentedToken = null;
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}