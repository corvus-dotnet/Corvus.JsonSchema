// <copyright file="ArazzoRunnerCatalogHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.Runner.Server.Models;
using Corvus.Text.Json.Arazzo.Durability.Runner.Server.Quotas;

namespace Corvus.Text.Json.Arazzo.Durability.Runner.Server;

/// <summary>
/// Implements the runner API's catalog operations: what a runner may execute, and the artifacts to execute it with.
/// </summary>
public sealed class ArazzoRunnerCatalogHandler : IApiCatalogHandler
{
    private readonly RunnerCatalogCoordinator coordinator;
    private readonly RunnerPrincipalAccessor principals;
    private readonly RunnerQuotaGate quotas;

    /// <summary>Initializes a new instance of the <see cref="ArazzoRunnerCatalogHandler"/> class.</summary>
    /// <param name="coordinator">The catalog-facing coordinator.</param>
    /// <param name="principals">Reads the authenticated machine principal from the current request.</param>
    /// <param name="quotas">Meters the deployment's catalog-read quota.</param>
    public ArazzoRunnerCatalogHandler(RunnerCatalogCoordinator coordinator, RunnerPrincipalAccessor principals, RunnerQuotaGate quotas)
    {
        ArgumentNullException.ThrowIfNull(coordinator);
        ArgumentNullException.ThrowIfNull(principals);
        ArgumentNullException.ThrowIfNull(quotas);

        this.coordinator = coordinator;
        this.principals = principals;
        this.quotas = quotas;
    }

    /// <inheritdoc/>
    public async ValueTask<ListHostedVersionsResult> HandleListHostedVersionsAsync(ListHostedVersionsParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        if (this.principals.Resolve() is not { } principal)
        {
            return ListHostedVersionsResult.Forbidden(RunnerProblems.NoPrincipal(), workspace);
        }

        if (await this.quotas.TryAcquireAsync(RunnerQuotaKind.Catalog, principal, 1, cancellationToken).ConfigureAwait(false) is { } refused)
        {
            return ListHostedVersionsResult.TooManyRequests(RunnerProblems.QuotaExceeded(refused), workspace, RunnerQuotaGate.RetryAfterSeconds(refused));
        }

        int? limit = parameters.Limit.IsNotUndefined() ? (int)parameters.Limit : null;

        // The token goes through as the JSON value it arrived as. It is opaque to this handler, and turning it into a
        // string on the way in and back on the way out would be two allocations to say nothing.
        (IReadOnlyList<HostedVersionRecord> versions, ReadOnlyMemory<byte> nextPageToken) =
            await this.coordinator.ListHostedAsync(principal, limit, JsonString.From(parameters.PageToken), cancellationToken).ConfigureAwait(false);

        return ListHostedVersionsResult.Ok(Project(versions, nextPageToken), workspace);
    }

    /// <inheritdoc/>
    public async ValueTask<GetVersionDocumentResult> HandleGetVersionDocumentAsync(GetVersionDocumentParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        if (this.principals.Resolve() is not { } principal)
        {
            return GetVersionDocumentResult.Forbidden(RunnerProblems.NoPrincipal(), workspace);
        }

        if (await this.quotas.TryAcquireAsync(RunnerQuotaKind.Catalog, principal, 1, cancellationToken).ConfigureAwait(false) is { } refused)
        {
            return GetVersionDocumentResult.TooManyRequests(RunnerProblems.QuotaExceeded(refused), workspace, RunnerQuotaGate.RetryAfterSeconds(refused));
        }

        ReadOnlyMemory<byte>? document = await this.coordinator.GetDocumentAsync(
            principal,
            (string)parameters.BaseWorkflowId,
            (int)parameters.VersionNumber,
            (string)parameters.DocumentName,
            cancellationToken).ConfigureAwait(false);

        if (document is not { } bytes)
        {
            // One answer for "no such document" and "not yours". Telling them apart would confirm which versions exist
            // outside the environments this runner serves.
            return GetVersionDocumentResult.NotFound(RunnerProblems.NoSuchDocument(), workspace);
        }

        return GetVersionDocumentResult.Ok(bytes);
    }

    /// <inheritdoc/>
    public async ValueTask<GetHostedVersionResult> HandleGetHostedVersionAsync(GetHostedVersionParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        if (this.principals.Resolve() is not { } principal)
        {
            return GetHostedVersionResult.Forbidden(RunnerProblems.NoPrincipal(), workspace);
        }

        if (await this.quotas.TryAcquireAsync(RunnerQuotaKind.Catalog, principal, 1, cancellationToken).ConfigureAwait(false) is { } refused)
        {
            return GetHostedVersionResult.TooManyRequests(RunnerProblems.QuotaExceeded(refused), workspace, RunnerQuotaGate.RetryAfterSeconds(refused));
        }

        string baseWorkflowId = (string)parameters.BaseWorkflowId;
        int versionNumber = (int)parameters.VersionNumber;

        if (await this.coordinator.GetHostedAsync(principal, baseWorkflowId, versionNumber, cancellationToken).ConfigureAwait(false) is not { } version)
        {
            // Same single answer as a document pull: not yours and not there are indistinguishable on purpose.
            return GetHostedVersionResult.NotFound(RunnerProblems.NoSuchDocument(), workspace);
        }

        return GetHostedVersionResult.Ok(
            HostedVersion.Build(
                baseWorkflowId: version.BaseWorkflowId,
                hash: version.Hash,
                versionNumber: version.VersionNumber),
            workspace);
    }

    // Threaded rather than captured: the versions are the builder's context, so projecting a page allocates no closure
    // however many it returns.
    private static HostedVersions.Source<IReadOnlyList<HostedVersionRecord>> Project(IReadOnlyList<HostedVersionRecord> versions, ReadOnlyMemory<byte> nextPageToken)
        => HostedVersions.Build(
            in versions,
            HostedVersions.HostedVersionArray.Build(
                in versions,
                static (in IReadOnlyList<HostedVersionRecord> source, ref HostedVersions.HostedVersionArray.Builder builder) =>
                {
                    foreach (HostedVersionRecord version in source)
                    {
                        builder.AddItem(HostedVersion.Build(
                            baseWorkflowId: version.BaseWorkflowId,
                            hash: version.Hash,
                            versionNumber: version.VersionNumber));
                    }
                }),
            nextPageToken.IsEmpty ? default : (Models.JsonString.Source)nextPageToken.Span);
}