// <copyright file="ArazzoControlPlaneNativeBuildsHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Publishing;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Microsoft.Extensions.Logging;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server;

/// <summary>
/// Implements the generated <see cref="IApiNativeBuildsHandler"/> over an <see cref="INativeBuildJobStore"/> — the
/// control-plane surface that enqueues and reports asynchronous Native-AOT builds of a workflow version's serverless
/// binary per (environment, runtime target) (ADR 0055). Enqueuing is gated by the <c>catalog:write</c> capability scope
/// and reading by <c>catalog:read</c>; each is additionally reach-gated to the workflow version — a caller who cannot
/// read the version gets 404, so a build for an out-of-reach version never leaks.
/// </summary>
/// <remarks>
/// Enqueuing is idempotent per target: re-requesting the same (environment, runtimeIdentifier) resets that job to Queued
/// (a rebuild), and a background <see cref="NativeBuildWorker"/> then drives it Queued -> Building -> Ready | Failed. The
/// persisted <see cref="NativeBuildJob"/> is congruent with the API <see cref="Models.NativeBuildView"/>, so a read
/// projects as a free whole-document re-wrap (<c>Models.NativeBuildView.From</c>) and the pooled documents are handed to
/// the request workspace rather than copied.
/// </remarks>
public sealed class ArazzoControlPlaneNativeBuildsHandler : IApiNativeBuildsHandler
{
    private const string ProblemBase = "https://corvus-oss.org/arazzo/control-plane/problems/";
    private const int CountCap = 100;

    // The audited resource kind for enqueuing a build on this surface (design §850).
    private const string TargetKind = "native-build";

    private readonly INativeBuildJobStore builds;
    private readonly ISecuredWorkflowCatalog catalog;
    private readonly ControlPlaneAccess access;
    private readonly ILogger? auditLogger;

    /// <summary>Initializes a new, unscoped instance (every request runs with <see cref="AccessContext.System"/>).</summary>
    /// <param name="builds">The native build-job store.</param>
    /// <param name="catalog">The workflow catalog (version existence + reach, gating every operation).</param>
    public ArazzoControlPlaneNativeBuildsHandler(INativeBuildJobStore builds, ISecuredWorkflowCatalog catalog)
        : this(builds, catalog, new ControlPlaneAccess())
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ArazzoControlPlaneNativeBuildsHandler"/> class.</summary>
    /// <param name="builds">The native build-job store.</param>
    /// <param name="catalog">The workflow catalog (version existence + reach, gating every operation).</param>
    /// <param name="access">Resolves the caller's <see cref="AccessContext"/> and deployment identity per request.</param>
    /// <param name="auditLogger">The governance audit sink, or <see langword="null"/> to not audit.</param>
    internal ArazzoControlPlaneNativeBuildsHandler(INativeBuildJobStore builds, ISecuredWorkflowCatalog catalog, ControlPlaneAccess access, ILogger? auditLogger = null)
    {
        ArgumentNullException.ThrowIfNull(builds);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(access);
        this.builds = builds;
        this.catalog = catalog;
        this.access = access;
        this.auditLogger = auditLogger;
    }

    /// <inheritdoc/>
    public async ValueTask<ListNativeBuildsResult> HandleListNativeBuildsAsync(ListNativeBuildsParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string baseWorkflowId = (string)parameters.BaseWorkflowId;
        int versionNumber = (int)parameters.VersionNumber;

        // Visibility: a version's builds are readable by anyone who can read the version itself.
        using (ParsedJsonDocument<CatalogVersion>? version = await this.catalog.GetAsync(baseWorkflowId, versionNumber, this.access.Current(), cancellationToken).ConfigureAwait(false))
        {
            if (version is null)
            {
                return ListNativeBuildsResult.NotFound(VersionNotFoundProblem(baseWorkflowId, versionNumber), workspace);
            }
        }

        int limit = parameters.Limit.IsNotUndefined() ? (int)parameters.Limit : 0;
        JsonString pageToken = JsonString.From(parameters.PageToken);
        var query = new NativeBuildJobQuery(ParseStatus(parameters.Status), baseWorkflowId, versionNumber);
        using NativeBuildJobPage page = await this.builds.ListAsync(query, limit, pageToken, cancellationToken).ConfigureAwait(false);

        // Built inline and consumed in place: NativeBuildList.Build scopes its result to the `in jobs` argument (and the
        // span-bound token), so it cannot be returned from a helper (CS8347). The rows re-wrap the pooled documents.
        page.Jobs.TransferOwnershipTo(workspace);
        IReadOnlyList<NativeBuildJob> jobs = page.Jobs;
        ReadOnlyMemory<byte> nextToken = page.NextPageToken;
        Models.NativeBuildList.Source<IReadOnlyList<NativeBuildJob>> body = Models.NativeBuildList.Build(
            in jobs,
            nativeBuilds: Models.NativeBuildList.NativeBuildViewArray.Build(in jobs, BuildViews),
            nextPageToken: nextToken.IsEmpty ? default : (Models.JsonString.Source)nextToken.Span);
        return ListNativeBuildsResult.Ok(body, workspace);
    }

    /// <inheritdoc/>
    public async ValueTask<EnqueueNativeBuildResult> HandleEnqueueNativeBuildAsync(EnqueueNativeBuildParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string baseWorkflowId = (string)parameters.BaseWorkflowId;
        int versionNumber = (int)parameters.VersionNumber;
        string environment = (string)parameters.Body.Environment;
        string runtimeIdentifier = (string)parameters.Body.RuntimeIdentifier;
        string? buildLabel = parameters.Body.BuildLabel.IsNotUndefined() ? (string)parameters.Body.BuildLabel : null;

        // Reach: the version must exist and be readable (404 otherwise); the caller's catalog:write scope gates capability.
        using (ParsedJsonDocument<CatalogVersion>? version = await this.catalog.GetAsync(baseWorkflowId, versionNumber, this.access.Current(), cancellationToken).ConfigureAwait(false))
        {
            if (version is null)
            {
                return EnqueueNativeBuildResult.NotFound(VersionNotFoundProblem(baseWorkflowId, versionNumber), workspace);
            }
        }

        AuditSubject enqueuedBy = this.CallerActor();
        using ParsedJsonDocument<NativeBuildJob> draft = NativeBuildJob.Draft(baseWorkflowId, versionNumber, environment, runtimeIdentifier, buildLabel);
        ParsedJsonDocument<NativeBuildJob> queued = await this.builds.EnqueueAsync(draft.RootElement, enqueuedBy, cancellationToken).ConfigureAwait(false);
        workspace.TakeOwnership(queued);
        GovernanceAudit.Mutation(this.auditLogger, "nativebuild.enqueue", enqueuedBy, TargetKind, queued.RootElement.IdValue, "queued");
        Models.NativeBuildView.Source body = Models.NativeBuildView.From(queued.RootElement);
        return EnqueueNativeBuildResult.Accepted(body, workspace);
    }

    /// <inheritdoc/>
    public async ValueTask<CountNativeBuildsResult> HandleCountNativeBuildsAsync(CountNativeBuildsParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string baseWorkflowId = (string)parameters.BaseWorkflowId;
        int versionNumber = (int)parameters.VersionNumber;

        // Same visibility gate as the list (readable by anyone who can read the version, 404 otherwise), minus paging —
        // the store returns only a bounded total, never rows.
        using (ParsedJsonDocument<CatalogVersion>? version = await this.catalog.GetAsync(baseWorkflowId, versionNumber, this.access.Current(), cancellationToken).ConfigureAwait(false))
        {
            if (version is null)
            {
                return CountNativeBuildsResult.NotFound(VersionNotFoundProblem(baseWorkflowId, versionNumber), workspace);
            }
        }

        var query = new NativeBuildJobQuery(ParseStatus(parameters.Status), baseWorkflowId, versionNumber);
        (int count, bool capped) = await this.builds.CountAsync(query, CountCap, cancellationToken).ConfigureAwait(false);
        return CountNativeBuildsResult.Ok(Models.CountResult.Build(capped: capped, count: count), workspace);
    }

    /// <inheritdoc/>
    public async ValueTask<GetNativeBuildResult> HandleGetNativeBuildAsync(GetNativeBuildParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string baseWorkflowId = (string)parameters.BaseWorkflowId;
        int versionNumber = (int)parameters.VersionNumber;
        string environment = (string)parameters.Environment;
        string runtimeIdentifier = (string)parameters.RuntimeIdentifier;

        // Visibility: the build is readable by anyone who can read the version (404 for an out-of-reach version).
        using (ParsedJsonDocument<CatalogVersion>? version = await this.catalog.GetAsync(baseWorkflowId, versionNumber, this.access.Current(), cancellationToken).ConfigureAwait(false))
        {
            if (version is null)
            {
                return GetNativeBuildResult.NotFound(VersionNotFoundProblem(baseWorkflowId, versionNumber), workspace);
            }
        }

        // The build id is derived from the target tuple, so the read is a point lookup.
        string id = NativeBuildJob.DeriveId(baseWorkflowId, versionNumber, environment, runtimeIdentifier);
        ParsedJsonDocument<NativeBuildJob>? job = await this.builds.GetAsync(id, cancellationToken).ConfigureAwait(false);
        if (job is null)
        {
            return GetNativeBuildResult.NotFound(BuildNotFoundProblem(baseWorkflowId, versionNumber, environment, runtimeIdentifier), workspace);
        }

        workspace.TakeOwnership(job);
        Models.NativeBuildView.Source body = Models.NativeBuildView.From(job.RootElement);
        return GetNativeBuildResult.Ok(body, workspace);
    }

    // Each build row is congruent with the persisted job — a free whole-document re-wrap (Models.NativeBuildView.From).
    // The views reference the pooled documents handed to the workspace by the caller.
    private static void BuildViews(in IReadOnlyList<NativeBuildJob> jobs, ref Models.NativeBuildList.NativeBuildViewArray.Builder array)
    {
        foreach (NativeBuildJob job in jobs)
        {
            array.AddItem(Models.NativeBuildView.From(job));
        }
    }

    // The status query filter, mapped from the API enum to the store's — string-free (the JSON value's bytes are compared
    // against the u8 wire names, no status string is realised), mirroring the store's own NativeBuildJob.HasStatus. An
    // absent filter matches anything.
    private static NativeBuildJobStatus? ParseStatus(Models.NativeBuildStatus status)
    {
        if (!status.IsNotUndefined())
        {
            return null;
        }

        if (status.ValueEquals("Queued"u8))
        {
            return NativeBuildJobStatus.Queued;
        }

        if (status.ValueEquals("Building"u8))
        {
            return NativeBuildJobStatus.Building;
        }

        if (status.ValueEquals("Ready"u8))
        {
            return NativeBuildJobStatus.Ready;
        }

        return status.ValueEquals("Failed"u8) ? NativeBuildJobStatus.Failed : null;
    }

    // The §850 audit subject: the authenticated principal who enqueued the build, falling back to the configured actor.
    private AuditSubject CallerActor() => this.access.AuditSubject();

    private static Models.ProblemDetails.Source VersionNotFoundProblem(string baseWorkflowId, int versionNumber)
        => Problem("version-not-found", "Workflow version not found", 404, $"No version {versionNumber} of workflow '{baseWorkflowId}' exists, or it is outside your reach.");

    private static Models.ProblemDetails.Source BuildNotFoundProblem(string baseWorkflowId, int versionNumber, string environment, string runtimeIdentifier)
        => Problem("native-build-not-found", "Native build not found", 404, $"No native build of version {versionNumber} of workflow '{baseWorkflowId}' exists for '{runtimeIdentifier}' in environment '{environment}'.");

    private static Models.ProblemDetails.Source Problem(string type, string title, int status, string detail)
        => Models.ProblemDetails.Build(
            detail: detail,
            status: status,
            title: title,
            type: ProblemBase + type);
}