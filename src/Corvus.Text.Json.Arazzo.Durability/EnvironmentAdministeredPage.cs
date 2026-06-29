// <copyright file="EnvironmentAdministeredPage.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// One keyset page of the reverse environment-administration index (design §7.8): the environment names a caller's
/// identity administers, ordered by <c>environmentName</c>, plus an opaque <see cref="NextPageToken"/> to fetch the next
/// page (empty when this is the last page). The <see cref="EnvironmentNames"/> are detached strings, so the page owns no
/// pooled documents; only the token (when present) is held in a pooled buffer — <see cref="Dispose"/> the page once read
/// to return it. Mirrors <see cref="WorkflowAdministeredPage"/>.
/// </summary>
/// <remarks>
/// The token is built bytes-native: <see cref="Create(IReadOnlyList{string}, ReadOnlySpan{byte})"/> Base64URL-encodes the
/// last row's environment name straight into a pooled buffer (no token string), exposed as <see cref="NextPageToken"/>
/// UTF-8 the handler writes verbatim into the response.
/// </remarks>
public sealed class EnvironmentAdministeredPage : IDisposable
{
    /// <summary>The page size used when a caller passes a non-positive limit — the store contract's default, shared by the
    /// in-memory pager and every backend's native keyset query so they page identically.</summary>
    public const int DefaultPageSize = 50;

    private byte[]? rentedToken;

    private EnvironmentAdministeredPage(IReadOnlyList<string> environmentNames, ReadOnlyMemory<byte> nextPageToken, byte[]? rentedToken)
    {
        this.EnvironmentNames = environmentNames;
        this.NextPageToken = nextPageToken;
        this.rentedToken = rentedToken;
    }

    /// <summary>Gets the page's environment names, ordered by <c>environmentName</c>.</summary>
    public IReadOnlyList<string> EnvironmentNames { get; }

    /// <summary>Gets the opaque continuation token (UTF-8) to fetch the next page, or empty if this is the last page.</summary>
    public ReadOnlyMemory<byte> NextPageToken { get; }

    /// <summary>Creates a last page (no continuation token).</summary>
    /// <param name="environmentNames">The page's environment names.</param>
    /// <returns>The page.</returns>
    public static EnvironmentAdministeredPage Create(IReadOnlyList<string> environmentNames)
        => new(environmentNames, default, null);

    /// <summary>Creates a page with a continuation token, Base64URL-encoding the last row's environment name (its UTF-8)
    /// straight into a pooled buffer (no intermediate token string).</summary>
    /// <param name="environmentNames">The page's environment names.</param>
    /// <param name="lastEnvironmentNameUtf8">The last row's environment name as UTF-8 (the keyset cursor the next page resumes after).</param>
    /// <returns>The page, owning the pooled token buffer.</returns>
    public static EnvironmentAdministeredPage Create(IReadOnlyList<string> environmentNames, ReadOnlySpan<byte> lastEnvironmentNameUtf8)
    {
        int maxLength = EnvironmentAdministeredContinuationToken.GetMaxEncodedLength(lastEnvironmentNameUtf8.Length);
        byte[] buffer = ArrayPool<byte>.Shared.Rent(maxLength);
        try
        {
            int written = EnvironmentAdministeredContinuationToken.EncodeToUtf8(lastEnvironmentNameUtf8, buffer);
            return new EnvironmentAdministeredPage(environmentNames, buffer.AsMemory(0, written), buffer);
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