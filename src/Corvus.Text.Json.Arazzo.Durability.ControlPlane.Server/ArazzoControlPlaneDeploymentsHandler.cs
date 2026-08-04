// <copyright file="ArazzoControlPlaneDeploymentsHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Publishing;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server;

/// <summary>
/// Implements the generated <see cref="IApiDeploymentsHandler"/> over an <see cref="IWorkflowDeploymentStore"/> — the
/// read-only control-plane surface that reports a workflow version's serverless deployments per (environment, runtime
/// target) (ADR 0055): the deployment of its signed native binary to a function platform, the resulting function invoke
/// URL, and the deployment's lifecycle state. Reading is gated by the <c>catalog:read</c> capability scope and reach-gated
/// to the workflow version — a caller who cannot read the version gets 404, so a deployment for an out-of-reach version
/// never leaks.
/// </summary>
/// <remarks>
/// The surface is read-only on the control plane: the deploy runs on the runner, which holds the environment's cloud
/// credentials (ADR 0059), and a background <c>WorkflowDeployWorker</c> drives each deployment Queued -> Deploying ->
/// Deployed | Failed; the control plane records and reports the state the runner drives.
/// The persisted <see cref="WorkflowDeployment"/> is congruent with the API <see cref="Models.DeploymentView"/>, so a read
/// projects as a free whole-document re-wrap (<c>Models.DeploymentView.From</c>) and the pooled documents are handed to the
/// request workspace rather than copied.
/// </remarks>
public sealed class ArazzoControlPlaneDeploymentsHandler : IApiDeploymentsHandler
{
    private const string ProblemBase = "https://corvus-oss.org/arazzo/control-plane/problems/";
    private const int CountCap = 100;

    private readonly IWorkflowDeploymentStore deployments;
    private readonly ISecuredWorkflowCatalog catalog;
    private readonly ControlPlaneAccess access;

    /// <summary>Initializes a new, unscoped instance (every request runs with <see cref="AccessContext.System"/>).</summary>
    /// <param name="deployments">The workflow-deployment store.</param>
    /// <param name="catalog">The workflow catalog (version existence + reach, gating every operation).</param>
    public ArazzoControlPlaneDeploymentsHandler(IWorkflowDeploymentStore deployments, ISecuredWorkflowCatalog catalog)
        : this(deployments, catalog, new ControlPlaneAccess())
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ArazzoControlPlaneDeploymentsHandler"/> class.</summary>
    /// <param name="deployments">The workflow-deployment store.</param>
    /// <param name="catalog">The workflow catalog (version existence + reach, gating every operation).</param>
    /// <param name="access">Resolves the caller's <see cref="AccessContext"/> per request.</param>
    internal ArazzoControlPlaneDeploymentsHandler(IWorkflowDeploymentStore deployments, ISecuredWorkflowCatalog catalog, ControlPlaneAccess access)
    {
        ArgumentNullException.ThrowIfNull(deployments);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(access);
        this.deployments = deployments;
        this.catalog = catalog;
        this.access = access;
    }

    /// <inheritdoc/>
    public async ValueTask<ListDeploymentsResult> HandleListDeploymentsAsync(ListDeploymentsParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string baseWorkflowId = (string)parameters.BaseWorkflowId;
        int versionNumber = (int)parameters.VersionNumber;

        // Visibility: a version's deployments are readable by anyone who can read the version itself.
        using (ParsedJsonDocument<CatalogVersion>? version = await this.catalog.GetAsync(baseWorkflowId, versionNumber, this.access.Current(), cancellationToken).ConfigureAwait(false))
        {
            if (version is null)
            {
                return ListDeploymentsResult.NotFound(VersionNotFoundProblem(baseWorkflowId, versionNumber), workspace);
            }
        }

        int limit = parameters.Limit.IsNotUndefined() ? (int)parameters.Limit : 0;
        JsonString pageToken = JsonString.From(parameters.PageToken);
        var query = new WorkflowDeploymentQuery(ParseStatus(parameters.Status), baseWorkflowId, versionNumber);
        using WorkflowDeploymentPage page = await this.deployments.ListAsync(query, limit, pageToken, cancellationToken).ConfigureAwait(false);

        // Built inline and consumed in place (as the native-builds list is): DeploymentList.Build scopes its result to the
        // `in rows` argument (and the span-bound token), so it cannot be returned from a helper (CS8347). The rows re-wrap
        // the pooled documents.
        page.Deployments.TransferOwnershipTo(workspace);
        IReadOnlyList<WorkflowDeployment> rows = page.Deployments;
        ReadOnlyMemory<byte> nextToken = page.NextPageToken;
        Models.DeploymentList.Source<IReadOnlyList<WorkflowDeployment>> body = Models.DeploymentList.Build(
            in rows,
            deployments: Models.DeploymentList.DeploymentViewArray.Build(in rows, BuildViews),
            nextPageToken: nextToken.IsEmpty ? default : (Models.JsonString.Source)nextToken.Span);
        return ListDeploymentsResult.Ok(body, workspace);
    }

    /// <inheritdoc/>
    public async ValueTask<CountDeploymentsResult> HandleCountDeploymentsAsync(CountDeploymentsParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string baseWorkflowId = (string)parameters.BaseWorkflowId;
        int versionNumber = (int)parameters.VersionNumber;

        // Same visibility gate as the list (readable by anyone who can read the version, 404 otherwise), minus paging — the
        // store returns only a bounded total, never rows.
        using (ParsedJsonDocument<CatalogVersion>? version = await this.catalog.GetAsync(baseWorkflowId, versionNumber, this.access.Current(), cancellationToken).ConfigureAwait(false))
        {
            if (version is null)
            {
                return CountDeploymentsResult.NotFound(VersionNotFoundProblem(baseWorkflowId, versionNumber), workspace);
            }
        }

        var query = new WorkflowDeploymentQuery(ParseStatus(parameters.Status), baseWorkflowId, versionNumber);
        (int count, bool capped) = await this.deployments.CountAsync(query, CountCap, cancellationToken).ConfigureAwait(false);
        return CountDeploymentsResult.Ok(Models.CountResult.Build(capped: capped, count: count), workspace);
    }

    /// <inheritdoc/>
    public async ValueTask<GetDeploymentResult> HandleGetDeploymentAsync(GetDeploymentParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string baseWorkflowId = (string)parameters.BaseWorkflowId;
        int versionNumber = (int)parameters.VersionNumber;
        string environment = (string)parameters.Environment;
        string runtimeIdentifier = (string)parameters.RuntimeIdentifier;

        // Visibility: the deployment is readable by anyone who can read the version (404 for an out-of-reach version).
        using (ParsedJsonDocument<CatalogVersion>? version = await this.catalog.GetAsync(baseWorkflowId, versionNumber, this.access.Current(), cancellationToken).ConfigureAwait(false))
        {
            if (version is null)
            {
                return GetDeploymentResult.NotFound(VersionNotFoundProblem(baseWorkflowId, versionNumber), workspace);
            }
        }

        // The deployment id is derived from the target tuple, so the read is a point lookup.
        string id = WorkflowDeployment.DeriveId(baseWorkflowId, versionNumber, environment, runtimeIdentifier);
        ParsedJsonDocument<WorkflowDeployment>? deployment = await this.deployments.GetAsync(id, cancellationToken).ConfigureAwait(false);
        if (deployment is null)
        {
            return GetDeploymentResult.NotFound(DeploymentNotFoundProblem(baseWorkflowId, versionNumber, environment, runtimeIdentifier), workspace);
        }

        workspace.TakeOwnership(deployment);
        Models.DeploymentView.Source body = Models.DeploymentView.From(deployment.RootElement);
        return GetDeploymentResult.Ok(body, workspace);
    }

    // Each deployment row is congruent with the persisted deployment — a free whole-document re-wrap
    // (Models.DeploymentView.From). The views reference the pooled documents handed to the workspace by the caller.
    private static void BuildViews(in IReadOnlyList<WorkflowDeployment> rows, ref Models.DeploymentList.DeploymentViewArray.Builder array)
    {
        foreach (WorkflowDeployment deployment in rows)
        {
            array.AddItem(Models.DeploymentView.From(deployment));
        }
    }

    // The status query filter, mapped from the API enum to the store's — string-free (the JSON value's bytes are compared
    // against the u8 wire names, no status string is realised), mirroring the store's own WorkflowDeployment.HasStatus. An
    // absent filter matches anything.
    private static WorkflowDeploymentStatus? ParseStatus(Models.DeploymentStatus status)
    {
        if (!status.IsNotUndefined())
        {
            return null;
        }

        if (status.ValueEquals("Queued"u8))
        {
            return WorkflowDeploymentStatus.Queued;
        }

        if (status.ValueEquals("Deploying"u8))
        {
            return WorkflowDeploymentStatus.Deploying;
        }

        if (status.ValueEquals("Deployed"u8))
        {
            return WorkflowDeploymentStatus.Deployed;
        }

        return status.ValueEquals("Failed"u8) ? WorkflowDeploymentStatus.Failed : null;
    }

    private static Models.ProblemDetails.Source VersionNotFoundProblem(string baseWorkflowId, int versionNumber)
        => Problem("version-not-found", "Workflow version not found", 404, $"No version {versionNumber} of workflow '{baseWorkflowId}' exists, or it is outside your reach.");

    private static Models.ProblemDetails.Source DeploymentNotFoundProblem(string baseWorkflowId, int versionNumber, string environment, string runtimeIdentifier)
        => Problem("deployment-not-found", "Deployment not found", 404, $"No deployment of version {versionNumber} of workflow '{baseWorkflowId}' exists for '{runtimeIdentifier}' in environment '{environment}'.");

    private static Models.ProblemDetails.Source Problem(string type, string title, int status, string detail)
        => Models.ProblemDetails.Build(
            detail: detail,
            status: status,
            title: title,
            type: ProblemBase + type);
}