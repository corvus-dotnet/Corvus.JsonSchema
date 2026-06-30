// <copyright file="EnvironmentRunnerAuthorizationPage.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;

namespace Corvus.Text.Json.Arazzo.Durability.RunnerAuthorization;

/// <summary>
/// One keyset page of environment-runner authorizations (design §5.5): the authorizations for the page, ordered by
/// <c>(environment, runnerId)</c>, plus an opaque <see cref="NextPageToken"/> to fetch the next page (empty when this is the
/// last page). The <see cref="Authorizations"/> are pooled documents the caller owns, and the token (when present) is held in
/// a pooled buffer — <see cref="Dispose"/> the page once read to return both to the pool. Mirrors
/// <see cref="Availability.AvailabilityRequestPage"/>.
/// </summary>
/// <remarks>
/// The token is built bytes-native:
/// <see cref="Create(PooledDocumentList{EnvironmentRunnerAuthorization}, ReadOnlySpan{byte}, ReadOnlySpan{byte})"/>
/// Base64URL-encodes the last row's <c>(environment, runnerId)</c> straight into a pooled buffer (no token string), exposed as
/// <see cref="NextPageToken"/> UTF-8 the handler writes verbatim into the response. The buffer outlives the synchronous
/// response build and is returned on <see cref="Dispose"/>.
/// </remarks>
public sealed class EnvironmentRunnerAuthorizationPage : IDisposable
{
    /// <summary>The page size used when a caller passes a non-positive limit — the store contract's default, shared by the
    /// in-memory pager and every backend's native keyset query so they page identically.</summary>
    public const int DefaultPageSize = 50;

    private byte[]? rentedToken;

    private EnvironmentRunnerAuthorizationPage(PooledDocumentList<EnvironmentRunnerAuthorization> authorizations, ReadOnlyMemory<byte> nextPageToken, byte[]? rentedToken)
    {
        this.Authorizations = authorizations;
        this.NextPageToken = nextPageToken;
        this.rentedToken = rentedToken;
    }

    /// <summary>Gets the page's authorizations, ordered by <c>(environment, runnerId)</c>.</summary>
    public PooledDocumentList<EnvironmentRunnerAuthorization> Authorizations { get; }

    /// <summary>Gets the opaque continuation token (UTF-8) to fetch the next page, or empty if this is the last page.</summary>
    public ReadOnlyMemory<byte> NextPageToken { get; }

    /// <summary>Creates a last page (no continuation token).</summary>
    /// <param name="authorizations">The page's authorizations.</param>
    /// <returns>The page.</returns>
    public static EnvironmentRunnerAuthorizationPage Create(PooledDocumentList<EnvironmentRunnerAuthorization> authorizations)
        => new(authorizations, default, null);

    /// <summary>Creates a page with a continuation token, encoding the last row's <c>(environment, runnerId)</c> straight into
    /// a pooled buffer (no intermediate token string, no key string).</summary>
    /// <param name="authorizations">The page's authorizations.</param>
    /// <param name="lastEnvironmentUtf8">The last row's environment as UTF-8 (the first keyset cursor part).</param>
    /// <param name="lastRunnerIdUtf8">The last row's runner id as UTF-8 (the keyset tie-breaker the next page resumes after).</param>
    /// <returns>The page, owning the pooled token buffer.</returns>
    public static EnvironmentRunnerAuthorizationPage Create(PooledDocumentList<EnvironmentRunnerAuthorization> authorizations, ReadOnlySpan<byte> lastEnvironmentUtf8, ReadOnlySpan<byte> lastRunnerIdUtf8)
    {
        int maxLength = EnvironmentRunnerAuthorizationContinuationToken.GetMaxEncodedLength(lastEnvironmentUtf8.Length, lastRunnerIdUtf8.Length);
        byte[] buffer = ArrayPool<byte>.Shared.Rent(maxLength);
        try
        {
            int written = EnvironmentRunnerAuthorizationContinuationToken.EncodeToUtf8(lastEnvironmentUtf8, lastRunnerIdUtf8, buffer);
            return new EnvironmentRunnerAuthorizationPage(authorizations, buffer.AsMemory(0, written), buffer);
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
        this.Authorizations.Dispose();
        if (this.rentedToken is { } buffer)
        {
            this.rentedToken = null;
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}