// <copyright file="RunnerCatalogCoordinator.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using Corvus.Text.Json.Arazzo.Durability.Availability;

namespace Corvus.Text.Json.Arazzo.Durability.Runner.Server;

/// <summary>
/// The catalog-facing half of the runner API (ADR 0065): what a runner may execute, and the executor artifacts for it.
/// A runner reaching these holds no catalog credential, so both answers are scoped to the environments its principal
/// is bound to.
/// </summary>
/// <remarks>
/// A runner used to read the catalog itself and treat everything in it as hostable. That needed a credential for the
/// whole catalog and gave a runner sight of versions it had no business executing. Being told what to host needs
/// neither, and is a smaller answer besides.
/// </remarks>
public sealed class RunnerCatalogCoordinator
{
    // The environment name and the encoded token both fit the stack in the common case.
    private const int StackThreshold = 256;

    private readonly IWorkflowCatalogStore catalog;
    private readonly IAvailabilityStore availability;
    private readonly IRunnerEnvironmentBindings bindings;
    private readonly RunnerApiOptions options;

    /// <summary>Initializes a new instance of the <see cref="RunnerCatalogCoordinator"/> class.</summary>
    /// <param name="catalog">The catalog the control plane owns.</param>
    /// <param name="availability">What each environment has made available.</param>
    /// <param name="bindings">Resolves the environments a machine principal may execute runs for.</param>
    /// <param name="options">The deployment's bounds; defaults are used when omitted.</param>
    public RunnerCatalogCoordinator(IWorkflowCatalogStore catalog, IAvailabilityStore availability, IRunnerEnvironmentBindings bindings, RunnerApiOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(availability);
        ArgumentNullException.ThrowIfNull(bindings);

        this.catalog = catalog;
        this.availability = availability;
        this.bindings = bindings;
        this.options = options ?? new RunnerApiOptions();
    }

    /// <summary>
    /// Lists the versions the principal may execute, one page at a time.
    /// </summary>
    /// <param name="principal">The authenticated machine principal.</param>
    /// <param name="limit">The page size the runner asked for, bounded by the deployment.</param>
    /// <param name="pageToken">The continuation token from a previous page, or undefined for the first.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The page, and the token for the next one; empty when the listing is exhausted.</returns>
    /// <remarks>
    /// The page token is the availability store's own token, passed through rather than wrapped. It already carries
    /// the environment alongside the row key, which is exactly what resuming across a runner's environments needs, so
    /// wrapping it would have meant encoding what it already said.
    /// </remarks>
    public async ValueTask<(IReadOnlyList<HostedVersionRecord> Versions, ReadOnlyMemory<byte> NextPageToken)> ListHostedAsync(string principal, int? limit, JsonString pageToken, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(principal);

        IReadOnlyList<string> environments = await this.bindings.ResolveAsync(principal, cancellationToken).ConfigureAwait(false);
        if (environments.Count == 0)
        {
            // Bound to nothing, so there is nothing to host. The same answer a pending or revoked runner gets, and it
            // discloses no more than that.
            return ([], default);
        }

        int wanted = this.options.BoundHostedPage(limit);
        int start = 0;
        if (pageToken.IsNotUndefined())
        {
            // Read the environment straight out of the token's UTF-8: the runner's token never becomes a string here.
            using UnescapedUtf8JsonString tokenUtf8 = pageToken.GetUtf8String();
            if (AvailabilityContinuationToken.TryDecode(tokenUtf8.Span, out (string BaseWorkflowId, int VersionNumber, string Environment) cursor))
            {
                start = IndexOf(environments, cursor.Environment);
                if (start < 0)
                {
                    // The environment the token named is no longer bound. Start again rather than silently skipping
                    // the environments after it.
                    start = 0;
                    pageToken = default;
                }
            }
        }

        var versions = new List<HostedVersionRecord>(wanted);
        for (int i = start; i < environments.Count; i++)
        {
            // Only the environment the token named resumes mid-listing; the rest start at their beginning. One store
            // page per environment per call: a page that comes back short because some rows were skipped is a normal
            // keyset result, and stopping there keeps the token the store's own rather than something reassembled here.
            JsonString inner = i == start ? pageToken : default;
            using AvailabilityPage page = await this.availability.ListByEnvironmentAsync(environments[i], wanted - versions.Count, inner, cancellationToken).ConfigureAwait(false);
            for (int entryIndex = 0; entryIndex < page.Entries.Count; entryIndex++)
            {
                AvailabilityEntry entry = page.Entries[entryIndex];
                if (await this.ProjectAsync(entry.BaseWorkflowIdValue, (int)entry.VersionNumber, cancellationToken).ConfigureAwait(false) is { } hosted)
                {
                    versions.Add(hosted);
                }
            }

            if (!page.NextPageToken.IsEmpty)
            {
                // Copied because the page owns the token's buffer and is about to return it to the pool.
                return (versions, page.NextPageToken.ToArray());
            }

            if (versions.Count >= wanted)
            {
                return i + 1 < environments.Count
                    ? (versions, StartOfEnvironment(environments[i + 1]))
                    : (versions, default);
            }
        }

        return (versions, default);
    }

    /// <summary>
    /// Reads one document of a version's package, if the principal may execute that version.
    /// </summary>
    /// <param name="principal">The authenticated machine principal.</param>
    /// <param name="baseWorkflowId">The unversioned workflow identity.</param>
    /// <param name="versionNumber">The version number.</param>
    /// <param name="documentName">The document to read.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The bytes, or <see langword="null"/> when the version is not available to this principal or the document is absent.</returns>
    /// <remarks>
    /// Both refusals answer <see langword="null"/> on purpose. Distinguishing "you may not have this" from "there is
    /// no such thing" would tell a runner which versions exist outside its environments.
    /// </remarks>
    public async ValueTask<ReadOnlyMemory<byte>?> GetDocumentAsync(string principal, string baseWorkflowId, int versionNumber, string documentName, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(principal);
        ArgumentException.ThrowIfNullOrEmpty(baseWorkflowId);
        ArgumentException.ThrowIfNullOrEmpty(documentName);

        if (!await this.IsAvailableToAsync(principal, baseWorkflowId, versionNumber, cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return await this.catalog.GetDocumentAsync(baseWorkflowId, versionNumber, documentName, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Reads one version the principal may execute.
    /// </summary>
    /// <param name="principal">The authenticated machine principal.</param>
    /// <param name="baseWorkflowId">The unversioned workflow identity.</param>
    /// <param name="versionNumber">The version number.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The version, or <see langword="null"/> when it is not available to this principal.</returns>
    public async ValueTask<HostedVersionRecord?> GetHostedAsync(string principal, string baseWorkflowId, int versionNumber, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(principal);
        ArgumentException.ThrowIfNullOrEmpty(baseWorkflowId);

        return await this.IsAvailableToAsync(principal, baseWorkflowId, versionNumber, cancellationToken).ConfigureAwait(false)
            ? await this.ProjectAsync(baseWorkflowId, versionNumber, cancellationToken).ConfigureAwait(false)
            : null;
    }

    /// <summary>
    /// Reads a version's content hash, if the principal may execute that version.
    /// </summary>
    /// <param name="principal">The authenticated machine principal.</param>
    /// <param name="baseWorkflowId">The unversioned workflow identity.</param>
    /// <param name="versionNumber">The version number.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The hash, or <see langword="null"/> when the version is not available to this principal.</returns>
    public async ValueTask<string?> GetHashAsync(string principal, string baseWorkflowId, int versionNumber, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(principal);
        ArgumentException.ThrowIfNullOrEmpty(baseWorkflowId);

        if (!await this.IsAvailableToAsync(principal, baseWorkflowId, versionNumber, cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        HostedVersionRecord? hosted = await this.ProjectAsync(baseWorkflowId, versionNumber, cancellationToken).ConfigureAwait(false);
        return hosted?.Hash;
    }

    private static int IndexOf(IReadOnlyList<string> environments, string environment)
    {
        for (int i = 0; i < environments.Count; i++)
        {
            if (string.Equals(environments[i], environment, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    // The cursor that means "the beginning of this environment": an empty base id and version zero sort before every
    // real row, and the by-environment listing seeks strictly past the cursor, so it yields the environment in full.
    private static ReadOnlyMemory<byte> StartOfEnvironment(string environment)
    {
        int max = AvailabilityContinuationToken.GetMaxEncodedLength(string.Empty, environment);
        byte[]? rented = max > StackThreshold ? ArrayPool<byte>.Shared.Rent(max) : null;
        Span<byte> buffer = rented ?? stackalloc byte[StackThreshold];
        try
        {
            int written = AvailabilityContinuationToken.EncodeToUtf8(string.Empty, 0, environment, buffer);
            return buffer[..written].ToArray();
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }
    }

    private async ValueTask<bool> IsAvailableToAsync(string principal, string baseWorkflowId, int versionNumber, CancellationToken cancellationToken)
    {
        IReadOnlyList<string> environments = await this.bindings.ResolveAsync(principal, cancellationToken).ConfigureAwait(false);
        foreach (string environment in environments)
        {
            using ParsedJsonDocument<AvailabilityEntry>? entry = await this.availability.GetAsync(baseWorkflowId, versionNumber, environment, cancellationToken).ConfigureAwait(false);
            if (entry is not null)
            {
                return true;
            }
        }

        return false;
    }

    private async ValueTask<HostedVersionRecord?> ProjectAsync(string baseWorkflowId, int versionNumber, CancellationToken cancellationToken)
    {
        // The version document is owned only long enough to read its hash; the record outlives it.
        using ParsedJsonDocument<CatalogVersion>? versionDoc = await this.catalog.GetAsync(baseWorkflowId, versionNumber, cancellationToken).ConfigureAwait(false);
        if (versionDoc is not { } doc)
        {
            // Available but no longer catalogued. Skipped rather than surfaced: a runner cannot execute it, and this is
            // the control plane's inconsistency to resolve, not the runner's to hear about.
            return null;
        }

        return new HostedVersionRecord(baseWorkflowId, versionNumber, (string)doc.RootElement.Hash);
    }
}