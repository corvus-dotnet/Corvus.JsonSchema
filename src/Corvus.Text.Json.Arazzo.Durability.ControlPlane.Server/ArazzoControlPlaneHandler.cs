// <copyright file="ArazzoControlPlaneHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo;
using Corvus.Text.Json.Arazzo.Durability;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server;

/// <summary>
/// Implements the generated <see cref="IApiRunsHandler"/> over an <see cref="IWorkflowManagementClient"/>,
/// mapping each control-plane REST operation onto the corresponding management-client call and projecting the
/// .NET result DTOs into the generated response models.
/// </summary>
public sealed class ArazzoControlPlaneHandler : IApiRunsHandler
{
    private const string ProblemBase = "https://corvus-oss.org/arazzo/control-plane/problems/";

    private readonly IWorkflowManagementClient management;

    /// <summary>Initializes a new instance of the <see cref="ArazzoControlPlaneHandler"/> class.</summary>
    /// <param name="management">The control-plane client the endpoints delegate to.</param>
    public ArazzoControlPlaneHandler(IWorkflowManagementClient management)
    {
        ArgumentNullException.ThrowIfNull(management);
        this.management = management;
    }

    /// <inheritdoc/>
    public async ValueTask<ListRunsResult> HandleListRunsAsync(ListRunsParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        WorkflowRunStatus? status = parameters.Status.IsNotUndefined() ? Enum.Parse<WorkflowRunStatus>((string)parameters.Status) : null;
        string? workflowId = parameters.WorkflowId.IsNotUndefined() ? (string)parameters.WorkflowId : null;
        int limit = parameters.Limit.IsNotUndefined() ? (int)parameters.Limit : 100;
        string? pageToken = parameters.PageToken.IsNotUndefined() ? (string)parameters.PageToken : null;
        DateTimeOffset? createdAfter = ParseInstant(parameters.CreatedAfter);
        DateTimeOffset? createdBefore = ParseInstant(parameters.CreatedBefore);
        DateTimeOffset? updatedAfter = ParseInstant(parameters.UpdatedAfter);
        DateTimeOffset? updatedBefore = ParseInstant(parameters.UpdatedBefore);
        string? correlationId = parameters.CorrelationId.IsNotUndefined() ? (string)parameters.CorrelationId : null;
        IReadOnlyList<string>? tags = ParseTags(parameters.Tag);

        WorkflowRunPage page = await this.management.ListAsync(
            new WorkflowQuery(status, workflowId, limit, pageToken, createdAfter, createdBefore, updatedAfter, updatedBefore, correlationId, tags),
            cancellationToken).ConfigureAwait(false);
        return ListRunsResult.Ok(BuildPage(page), workspace);
    }

    private static DateTimeOffset? ParseInstant(Models.JsonDateTime value)
        => value.IsNotUndefined()
            ? DateTimeOffset.Parse((string)value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
            : null;

    private static IReadOnlyList<string>? ParseTags(Models.TagList tag)
    {
        if (tag.IsUndefined())
        {
            return null;
        }

        var tags = new List<string>();
        foreach (Models.JsonString item in tag.EnumerateArray())
        {
            tags.Add((string)item);
        }

        return tags.Count > 0 ? tags : null;
    }

    /// <inheritdoc/>
    public async ValueTask<GetRunResult> HandleGetRunAsync(GetRunParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string runId = (string)parameters.RunId;
        WorkflowRunDetail? detail = await this.management.GetAsync(runId, cancellationToken).ConfigureAwait(false);
        return detail is { } d
            ? GetRunResult.Ok(BuildDetail(d), workspace)
            : GetRunResult.NotFound(NotFoundProblem(runId), workspace);
    }

    /// <inheritdoc/>
    public async ValueTask<DeleteRunResult> HandleDeleteRunAsync(DeleteRunParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string runId = (string)parameters.RunId;
        WorkflowRunDetail? detail = await this.management.GetAsync(runId, cancellationToken).ConfigureAwait(false);
        if (detail is null)
        {
            return DeleteRunResult.NotFound(NotFoundProblem(runId), workspace);
        }

        return await this.management.DeleteAsync(runId, cancellationToken).ConfigureAwait(false)
            ? DeleteRunResult.NoContent()
            : DeleteRunResult.Conflict(Problem("not-deletable", "Run is not deletable", 409, $"Run '{runId}' is held by another owner; retry."), workspace);
    }

    /// <inheritdoc/>
    public async ValueTask<ResumeRunResult> HandleResumeRunAsync(ResumeRunParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string runId = (string)parameters.RunId;
        ResumeOptions options = ToResumeOptions(parameters.Body);
        if (await this.management.ResumeAsync(runId, options, cancellationToken).ConfigureAwait(false))
        {
            WorkflowRunDetail? resumed = await this.management.GetAsync(runId, cancellationToken).ConfigureAwait(false);
            return resumed is { } d
                ? ResumeRunResult.Ok(BuildDetail(d), workspace)
                : ResumeRunResult.NotFound(NotFoundProblem(runId), workspace);
        }

        WorkflowRunDetail? current = await this.management.GetAsync(runId, cancellationToken).ConfigureAwait(false);
        return current is { } existing
            ? ResumeRunResult.Conflict(Problem("not-resumable", "Run is not resumable", 409, $"Run '{runId}' is {existing.Status}; only a Faulted run can be resumed (it may also be held by another owner)."), workspace)
            : ResumeRunResult.NotFound(NotFoundProblem(runId), workspace);
    }

    /// <inheritdoc/>
    public async ValueTask<CancelRunResult> HandleCancelRunAsync(CancelRunParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string runId = (string)parameters.RunId;
        string reason = (string)parameters.Body.Reason;
        if (await this.management.CancelAsync(runId, reason, cancellationToken).ConfigureAwait(false))
        {
            WorkflowRunDetail? cancelled = await this.management.GetAsync(runId, cancellationToken).ConfigureAwait(false);
            return cancelled is { } d
                ? CancelRunResult.Ok(BuildDetail(d), workspace)
                : CancelRunResult.NotFound(NotFoundProblem(runId), workspace);
        }

        WorkflowRunDetail? current = await this.management.GetAsync(runId, cancellationToken).ConfigureAwait(false);
        return current is { } existing
            ? CancelRunResult.Conflict(Problem("not-cancellable", "Run is not cancellable", 409, $"Run '{runId}' is {existing.Status}; a terminal run cannot be cancelled (it may also be held by another owner)."), workspace)
            : CancelRunResult.NotFound(NotFoundProblem(runId), workspace);
    }

    /// <inheritdoc/>
    public async ValueTask<PurgeRunsResult> HandlePurgeRunsAsync(PurgeRunsParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        var olderThan = DateTimeOffset.Parse((string)parameters.OlderThan, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        int limit = parameters.Limit.IsNotUndefined() ? (int)parameters.Limit : 1000;
        int purged = await this.management.PurgeAsync(new WorkflowPurgeQuery(olderThan, limit), cancellationToken).ConfigureAwait(false);
        return PurgeRunsResult.Ok(
            new Models.PurgeResult.Source((ref Models.PurgeResult.Builder b) => b.Create(purgedCount: purged)),
            workspace);
    }

    // Map the generated ResumeRequest union onto the engine's ResumeOptions by matching the variant the
    // `mode` const selected. An absent body (the request body is optional) means a plain retry.
    private static ResumeOptions ToResumeOptions(in Models.ResumeRequest body)
    {
        if (body.ValueKind != JsonValueKind.Object)
        {
            return ResumeOptions.RetryFaultedStep;
        }

        return body.Match(
            static (in Models.RetryFaultedStepResume _) => ResumeOptions.RetryFaultedStep,
            static (in Models.RewindResume r) => ResumeOptions.Rewind(r.TargetCursor),
            static (in Models.SkipResume r) => ResumeOptions.Skip(
                r.SkipOutputs,
                r.TargetCursor.IsNotUndefined() ? r.TargetCursor : (int?)null),
            static (in Models.StatePatchResume r) => ResumeOptions.StatePatch(r.Patch),
            static (in Models.ResumeRequest _) => ResumeOptions.RetryFaultedStep);
    }

    private static Models.WorkflowRunDetail.Source BuildDetail(WorkflowRunDetail d)
        => new((ref Models.WorkflowRunDetail.Builder b) =>
        {
            Models.WorkflowFault.Source fault = default;
            if (d.Fault is { } f)
            {
                fault = BuildFault(f);
            }

            Models.WorkflowWait.Source wait = default;
            if (d.Wait is { } w)
            {
                wait = BuildWait(w);
            }

            Models.JsonString.Source correlationId = default;
            if (d.CorrelationId is { } cid)
            {
                correlationId = cid;
            }

            Models.WorkflowRunDetail.JsonStringArray.Source tags = default;
            if (d.Tags is { Count: > 0 } runTags)
            {
                tags = new Models.WorkflowRunDetail.JsonStringArray.Source(
                    (ref Models.WorkflowRunDetail.JsonStringArray.Builder ab) =>
                    {
                        foreach (string tag in runTags)
                        {
                            ab.AddItem(tag);
                        }
                    });
            }

            b.Create(
                createdAt: d.CreatedAt,
                cursor: d.Cursor,
                etag: d.Etag.Value ?? string.Empty,
                id: d.Id.Value,
                status: d.Status.ToString(),
                workflowId: d.WorkflowId,
                correlationId: correlationId,
                fault: fault,
                tags: tags,
                wait: wait);
        });

    private static Models.WorkflowRunPage.Source BuildPage(WorkflowRunPage page)
        => new((ref Models.WorkflowRunPage.Builder b) =>
        {
            Models.JsonString.Source nextPageToken = default;
            if (page.ContinuationToken is { } token)
            {
                nextPageToken = token;
            }

            b.Create(
                runs: new Models.WorkflowRunPage.WorkflowRunSummaryArray.Source(
                    (ref Models.WorkflowRunPage.WorkflowRunSummaryArray.Builder ab) =>
                    {
                        foreach (WorkflowRunListing listing in page.Runs)
                        {
                            ab.AddItem(BuildSummary(listing));
                        }
                    }),
                nextPageToken: nextPageToken);
        });

    private static Models.WorkflowRunSummary.Source BuildSummary(WorkflowRunListing listing)
        => new((ref Models.WorkflowRunSummary.Builder b) =>
        {
            WorkflowRunIndexEntry e = listing.Index;

            Models.JsonString.Source awaitingChannel = default;
            if (e.AwaitingChannel is { } ac)
            {
                awaitingChannel = ac;
            }

            Models.JsonString.Source awaitingCorrelationId = default;
            if (e.AwaitingCorrelationId is { } acid)
            {
                awaitingCorrelationId = acid;
            }

            Models.JsonDateTime.Source dueAt = default;
            if (e.DueAt is { } due)
            {
                dueAt = due;
            }

            Models.JsonString.Source errorType = default;
            if (e.ErrorType is { } et)
            {
                errorType = et;
            }

            Models.JsonString.Source correlationId = default;
            if (e.CorrelationId is { } cid)
            {
                correlationId = cid;
            }

            Models.WorkflowRunSummary.JsonStringArray.Source tags = default;
            if (e.Tags is { Count: > 0 } runTags)
            {
                tags = new Models.WorkflowRunSummary.JsonStringArray.Source(
                    (ref Models.WorkflowRunSummary.JsonStringArray.Builder ab) =>
                    {
                        foreach (string tag in runTags)
                        {
                            ab.AddItem(tag);
                        }
                    });
            }

            b.Create(
                createdAt: e.CreatedAt,
                id: listing.Id.Value,
                status: e.Status.ToString(),
                updatedAt: e.UpdatedAt,
                workflowId: e.WorkflowId,
                awaitingChannel: awaitingChannel,
                awaitingCorrelationId: awaitingCorrelationId,
                correlationId: correlationId,
                dueAt: dueAt,
                errorType: errorType,
                tags: tags);
        });

    private static Models.WorkflowFault.Source BuildFault(WorkflowFault f)
        => new((ref Models.WorkflowFault.Builder b) => b.Create(at: f.At, attempt: f.Attempt, error: f.Error, stepId: f.StepId));

    private static Models.WorkflowWait.Source BuildWait(WorkflowWait w)
        => new((ref Models.WorkflowWait.Builder b) =>
        {
            Models.JsonString.Source channel = default;
            if (w.Channel is { } ch)
            {
                channel = ch;
            }

            Models.JsonString.Source correlationId = default;
            if (w.CorrelationId is { } corr)
            {
                correlationId = corr;
            }

            Models.JsonDateTime.Source dueAt = default;
            if (w.DueAt is { } due)
            {
                dueAt = due;
            }

            b.Create(kind: w.Kind.ToString(), channel: channel, correlationId: correlationId, dueAt: dueAt);
        });

    private static Models.ProblemDetails.Source NotFoundProblem(string runId)
        => Problem("run-not-found", "Run not found", 404, $"No run with id '{runId}' exists.");

    private static Models.ProblemDetails.Source Problem(string type, string title, int status, string detail)
        => new((ref Models.ProblemDetails.Builder b) => b.Create(
            detail: detail,
            status: status,
            title: title,
            type: ProblemBase + type));
}