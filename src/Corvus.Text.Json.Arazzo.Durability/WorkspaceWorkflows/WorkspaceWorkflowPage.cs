// <copyright file="WorkspaceWorkflowPage.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;

namespace Corvus.Text.Json.Arazzo.Durability.WorkspaceWorkflows;

/// <summary>
/// One keyset page of working copies (workflow-designer design §4.1): the reach-visible working copies for the page, ordered by <c>id</c>, plus an
/// opaque <see cref="NextPageToken"/> to fetch the next page (empty when this is the last page). The <see cref="WorkingCopies"/>
/// are pooled documents the caller owns (each the full stored working copy, including its document — the handler field-selects
/// the summary), and the token (when present) is held in a pooled buffer — <see cref="Dispose"/> the page once read to
/// return both to the pool.
/// </summary>
/// <remarks>
/// The token is built bytes-native: <see cref="Create(PooledDocumentList{WorkspaceWorkflow}, string, string)"/> Base64URL-encodes
/// the cursor straight into a pooled buffer (no token string), exposed as <see cref="NextPageToken"/> UTF-8 the handler
/// writes verbatim into the response. The buffer outlives the synchronous response build and is returned on
/// <see cref="Dispose"/>. A carrier that owns a rented buffer is a <see langword="sealed"/> <see langword="class"/>,
/// never a record struct (a value-copy would double-return the rent).
/// </remarks>
public sealed class WorkspaceWorkflowPage : IDisposable
{
    private byte[]? rentedToken;

    private WorkspaceWorkflowPage(PooledDocumentList<WorkspaceWorkflow> workingCopies, ReadOnlyMemory<byte> nextPageToken, byte[]? rentedToken)
    {
        this.WorkingCopies = workingCopies;
        this.NextPageToken = nextPageToken;
        this.rentedToken = rentedToken;
    }

    /// <summary>Gets the page's reach-visible working copies, ordered by <c>id</c>.</summary>
    public PooledDocumentList<WorkspaceWorkflow> WorkingCopies { get; }

    /// <summary>Gets the opaque continuation token (UTF-8) to fetch the next page, or empty if this is the last page.</summary>
    public ReadOnlyMemory<byte> NextPageToken { get; }

    /// <summary>Creates a last page (no continuation token).</summary>
    /// <param name="workingCopies">The page's reach-visible working copies.</param>
    /// <returns>The page.</returns>
    public static WorkspaceWorkflowPage Create(PooledDocumentList<WorkspaceWorkflow> workingCopies)
        => new(workingCopies, default, null);

    /// <summary>Creates a page with a continuation token, encoding the last row's <c>(id, tie-breaker)</c> cursor
    /// straight into a pooled buffer (no intermediate token string).</summary>
    /// <param name="workingCopies">The page's reach-visible working copies.</param>
    /// <param name="id">The last row's working-copy id.</param>
    /// <param name="tieBreaker">The last row's tie-breaker (the tag discriminator).</param>
    /// <returns>The page, owning the pooled token buffer.</returns>
    public static WorkspaceWorkflowPage Create(PooledDocumentList<WorkspaceWorkflow> workingCopies, string id, string tieBreaker)
    {
        int maxLength = WorkspaceWorkflowContinuationToken.GetMaxEncodedLength(id, tieBreaker);
        byte[] buffer = ArrayPool<byte>.Shared.Rent(maxLength);
        try
        {
            int written = WorkspaceWorkflowContinuationToken.EncodeToUtf8(id, tieBreaker, buffer);
            return new WorkspaceWorkflowPage(workingCopies, buffer.AsMemory(0, written), buffer);
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
        this.WorkingCopies.Dispose();
        if (this.rentedToken is { } buffer)
        {
            this.rentedToken = null;
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}