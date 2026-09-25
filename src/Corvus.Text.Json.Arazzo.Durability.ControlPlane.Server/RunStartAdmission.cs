// <copyright file="RunStartAdmission.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server;

/// <summary>
/// Admits and starts a run of a catalogued workflow version: the one seam every way of starting a run goes through.
/// </summary>
/// <remarks>
/// Starting a run owes a chain of checks (reach, tenancy agreement, a runnable version, valid inputs, availability in
/// the environment, a hosting runner at the environment's isolation, a live deployment where one is required, and the
/// tenant's capacity). A start path that does not come through here has skipped some of them. The catalog's start and
/// a run's re-run both do.
/// </remarks>
public interface IRunStartAdmission
{
    /// <summary>Runs the admission chain for the current caller and, if it admits, starts the run.</summary>
    /// <param name="request">What to start.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The run that was started, or why none was.</returns>
    ValueTask<RunStartOutcome> AdmitAndStartAsync(RunStartRequest request, CancellationToken cancellationToken);
}

/// <summary>A request to start a run of a catalogued workflow version.</summary>
/// <param name="BaseWorkflowId">The base workflow id.</param>
/// <param name="VersionNumber">The version to run.</param>
/// <param name="Environment">The environment the run is pinned to. A request without one is refused.</param>
/// <param name="Inputs">The workflow inputs, validated against the version's inputs schema by the chain.</param>
/// <param name="IdempotencyKey">An idempotency key, or <see langword="null"/> or empty for an ordinary one-shot start.</param>
/// <param name="Tags">The free-form tags to stamp on the run.</param>
/// <param name="RerunOf">The run this start re-runs, or <see langword="null"/>.</param>
/// <param name="AuditAction">The governance-audit action the start is recorded under.</param>
/// <param name="SealedRunId">For a sealed start (ADR 0065 decision 9), the run id the initiator chose; <see langword="null"/> for a plain start.</param>
/// <param name="Sealed">For a sealed start, the initiator's seal, which the chain stores unread in place of validating <paramref name="Inputs"/>; <see langword="null"/> for a plain start.</param>
public readonly record struct RunStartRequest(
    string BaseWorkflowId,
    int VersionNumber,
    string? Environment,
    JsonElement Inputs,
    string? IdempotencyKey = null,
    TagSet Tags = default,
    string? RerunOf = null,
    string AuditAction = "run.start",
    WorkflowRunId? SealedRunId = null,
    SealedInputs? Sealed = null)
{
    /// <summary>Gets a value indicating whether this is a sealed start: the initiator named the run and sealed the inputs.</summary>
    public bool IsSealed => this.Sealed is not null && this.SealedRunId is not null;
}

/// <summary>How an admission ended.</summary>
public enum RunStartOutcomeKind
{
    /// <summary>The run was started, or an idempotent start found it already started.</summary>
    Accepted,

    /// <summary>The version is not in the catalog, or is outside the caller's read reach.</summary>
    VersionNotFound,

    /// <summary>The chain refused the start, for the reason the outcome carries.</summary>
    Refused,

    /// <summary>The inputs do not validate against the version's inputs schema.</summary>
    InvalidInputs,

    /// <summary>The tenant is at a standing capacity limit.</summary>
    CapacityExceeded,
}

/// <summary>
/// What an admission decided, in terms no one operation owns, so that each caller answers in its own response type.
/// </summary>
/// <param name="Kind">How the admission ended.</param>
/// <param name="RunId">The started run, when <see cref="RunStartOutcomeKind.Accepted"/>.</param>
/// <param name="WorkflowId">The versioned workflow id the run executes, when accepted.</param>
/// <param name="Status">The HTTP status of a refusal: 404 or 409.</param>
/// <param name="ProblemType">The problem type of a refusal.</param>
/// <param name="Title">The problem title of a refusal.</param>
/// <param name="Detail">The problem detail of a refusal.</param>
/// <param name="Errors">The validation errors, when <see cref="RunStartOutcomeKind.InvalidInputs"/>.</param>
/// <param name="Capacity">The limit that was reached, when <see cref="RunStartOutcomeKind.CapacityExceeded"/>.</param>
public readonly record struct RunStartOutcome(
    RunStartOutcomeKind Kind,
    WorkflowRunId RunId = default,
    string? WorkflowId = null,
    int Status = 0,
    string? ProblemType = null,
    string? Title = null,
    string? Detail = null,
    IReadOnlyList<(string InstancePath, string Message, string SchemaLocation)>? Errors = null,
    Capacity.ControlPlaneCapacityRejection? Capacity = null)
{
    /// <summary>The run was started.</summary>
    /// <param name="runId">The run.</param>
    /// <param name="workflowId">The versioned workflow id it executes.</param>
    /// <returns>The outcome.</returns>
    public static RunStartOutcome Accepted(WorkflowRunId runId, string workflowId) => new(RunStartOutcomeKind.Accepted, runId, workflowId);

    /// <summary>The version is not in the catalog, or is outside the caller's read reach.</summary>
    /// <returns>The outcome.</returns>
    public static RunStartOutcome VersionNotFound() => new(RunStartOutcomeKind.VersionNotFound, Status: 404);

    /// <summary>The chain refused the start.</summary>
    /// <param name="status">404 or 409.</param>
    /// <param name="problemType">The problem type.</param>
    /// <param name="title">The problem title.</param>
    /// <param name="detail">The problem detail.</param>
    /// <returns>The outcome.</returns>
    public static RunStartOutcome Refused(int status, string problemType, string title, string detail)
        => new(RunStartOutcomeKind.Refused, Status: status, ProblemType: problemType, Title: title, Detail: detail);

    /// <summary>The inputs do not validate.</summary>
    /// <param name="errors">The validation errors.</param>
    /// <returns>The outcome.</returns>
    public static RunStartOutcome InvalidInputs(IReadOnlyList<(string InstancePath, string Message, string SchemaLocation)> errors)
        => new(RunStartOutcomeKind.InvalidInputs, Status: 422, Errors: errors);

    /// <summary>The tenant is at a standing capacity limit.</summary>
    /// <param name="rejection">The limit that was reached.</param>
    /// <returns>The outcome.</returns>
    public static RunStartOutcome CapacityExceeded(Capacity.ControlPlaneCapacityRejection rejection)
        => new(RunStartOutcomeKind.CapacityExceeded, Status: 429, Capacity: rejection);

    /// <summary>Builds the accepted-run response body every start answers with.</summary>
    /// <param name="outcome">An accepted outcome.</param>
    /// <returns>The model source.</returns>
    internal static Models.WorkflowRunAccepted.Source AcceptedBody(RunStartOutcome outcome)
        => Models.WorkflowRunAccepted.Build(
            runId: outcome.RunId.Value,
            status: WorkflowRunStatus.Pending.ToString(),
            workflowId: outcome.WorkflowId!);
}