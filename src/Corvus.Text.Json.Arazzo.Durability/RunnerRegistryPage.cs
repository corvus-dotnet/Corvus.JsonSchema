// <copyright file="RunnerRegistryPage.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// One keyset page of registered runners: the runners for the page, ordered by <c>runnerId</c>, plus an opaque
/// <see cref="NextPageToken"/> to fetch the next page (empty when this is the last page). The <see cref="Runners"/> are
/// detached <see cref="RunnerRegistration"/> values (self-contained — <see cref="IRunnerRegistry.ListAsync(System.Threading.CancellationToken)"/>
/// detaches each from any store-side buffer), so the page owns no pooled documents; only the token (when present) is held
/// in a pooled buffer — <see cref="Dispose"/> the page once read to return it.
/// </summary>
/// <remarks>
/// The token is built bytes-native: <see cref="Create(IReadOnlyList{RunnerRegistration}, ReadOnlySpan{byte})"/>
/// Base64URL-encodes the last row's runner id straight into a pooled buffer (no token string), exposed as
/// <see cref="NextPageToken"/> UTF-8 the handler writes verbatim into the response.
/// </remarks>
public sealed class RunnerRegistryPage : IDisposable
{
    private byte[]? rentedToken;

    private RunnerRegistryPage(IReadOnlyList<RunnerRegistration> runners, ReadOnlyMemory<byte> nextPageToken, byte[]? rentedToken)
    {
        this.Runners = runners;
        this.NextPageToken = nextPageToken;
        this.rentedToken = rentedToken;
    }

    /// <summary>Gets the page's runners, ordered by <c>runnerId</c>.</summary>
    public IReadOnlyList<RunnerRegistration> Runners { get; }

    /// <summary>Gets the opaque continuation token (UTF-8) to fetch the next page, or empty if this is the last page.</summary>
    public ReadOnlyMemory<byte> NextPageToken { get; }

    /// <summary>Creates a last page (no continuation token).</summary>
    /// <param name="runners">The page's runners.</param>
    /// <returns>The page.</returns>
    public static RunnerRegistryPage Create(IReadOnlyList<RunnerRegistration> runners)
        => new(runners, default, null);

    /// <summary>Creates a page with a continuation token, Base64URL-encoding the last row's runner id (its UTF-8) straight
    /// into a pooled buffer (no intermediate token string, no id string).</summary>
    /// <param name="runners">The page's runners.</param>
    /// <param name="lastRunnerIdUtf8">The last row's runner id as UTF-8 (the keyset cursor the next page resumes after).</param>
    /// <returns>The page, owning the pooled token buffer.</returns>
    public static RunnerRegistryPage Create(IReadOnlyList<RunnerRegistration> runners, ReadOnlySpan<byte> lastRunnerIdUtf8)
    {
        int maxLength = RunnerRegistryContinuationToken.GetMaxEncodedLength(lastRunnerIdUtf8.Length);
        byte[] buffer = ArrayPool<byte>.Shared.Rent(maxLength);
        try
        {
            int written = RunnerRegistryContinuationToken.EncodeToUtf8(lastRunnerIdUtf8, buffer);
            return new RunnerRegistryPage(runners, buffer.AsMemory(0, written), buffer);
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