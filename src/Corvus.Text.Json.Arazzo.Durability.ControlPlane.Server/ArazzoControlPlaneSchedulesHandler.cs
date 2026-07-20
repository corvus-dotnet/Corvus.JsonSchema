// <copyright file="ArazzoControlPlaneSchedulesHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Availability;
using Corvus.Text.Json.Arazzo.Durability.Environments;
using Microsoft.Extensions.Logging;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server;

/// <summary>
/// Implements the generated <see cref="IApiSchedulesHandler"/> (#896): the first-class management surface over durable
/// schedules. A schedule is a durable run of the built-in scheduler workflow (the reserved <c>$schedule</c> id) that
/// fires a target version on a cron cadence through the governed run endpoint; these operations create, list, read,
/// cancel, and immediately run those scheduler runs, projecting each as a <see cref="Models.Schedule"/> rather than a
/// raw run. Reach- and scope-gated exactly as the runs surface is (<c>runs:read</c>/<c>runs:write</c>).
/// </summary>
/// <remarks>
/// Allocation ownership. The projection source is JSON (a scheduler run's <see cref="WorkflowCheckpointState.Inputs"/>,
/// a <see cref="WorkflowScheduleInput"/>), so each response field is <b>value-bridged</b> bytes-to-bytes
/// (<c>Models.JsonString.From(spec.X)</c>) rather than realised through a managed string. The loaded checkpoint states
/// are held alive across the <b>synchronous</b> response build (<c>Result.Ok</c> calls <c>CreateBuilder</c> at once)
/// and disposed immediately after. A managed string is realised only where a downstream seam is itself string-typed:
/// the <c>scheduleId</c>/<c>environment</c>/target ids the catalog and management APIs take, the cron expression Cronos
/// parses, and the run status enum. The last-fired watermark is read through the CTJ typed <c>TryGetDateTimeOffset</c>
/// accessor (no <c>GetString</c> + parse), and the scheduler run's inputs are built by typed construction.
/// </remarks>
public sealed class ArazzoControlPlaneSchedulesHandler : IApiSchedulesHandler
{
    private const string ProblemBase = "https://corvus-oss.org/arazzo/control-plane/problems/";
    private const string ScheduleWorkflowId = ScheduleHostedWorkflow.ScheduleWorkflowId;
    private const string TargetKind = "schedule";

    private readonly ISecuredWorkflowManagement management;
    private readonly ISecuredWorkflowCatalog catalog;
    private readonly IRunnerRegistry runners;
    private readonly IAvailabilityStore? availabilityStore;
    private readonly IEnvironmentStore? environmentStore;
    private readonly ControlPlaneAccess access;
    private readonly TimeProvider timeProvider;
    private readonly ILogger? auditLogger;

    /// <summary>Initializes a new instance of the <see cref="ArazzoControlPlaneSchedulesHandler"/> class.</summary>
    /// <param name="management">The management client that creates, reads, and cancels the scheduler runs.</param>
    /// <param name="catalog">The catalog client consulted to validate (and reach-gate) a schedule's target version.</param>
    /// <param name="runners">The runner registry consulted for a hosting runner and a scheduling-capable runner in the environment.</param>
    /// <param name="access">Resolves the caller's <see cref="AccessContext"/> per request (§14.2).</param>
    /// <param name="availabilityStore">The availability registry used to validate the target is available in the environment (§7.8); <see langword="null"/> skips it.</param>
    /// <param name="environmentStore">The environment registry (reserved for future environment validation); currently unused.</param>
    /// <param name="timeProvider">The clock used to compute each schedule's next occurrence; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="auditLogger">The governance-audit sink for schedule mutations.</param>
    internal ArazzoControlPlaneSchedulesHandler(
        ISecuredWorkflowManagement management,
        ISecuredWorkflowCatalog catalog,
        IRunnerRegistry runners,
        ControlPlaneAccess access,
        IAvailabilityStore? availabilityStore = null,
        IEnvironmentStore? environmentStore = null,
        TimeProvider? timeProvider = null,
        ILogger? auditLogger = null)
    {
        ArgumentNullException.ThrowIfNull(management);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(runners);
        ArgumentNullException.ThrowIfNull(access);
        this.management = management;
        this.catalog = catalog;
        this.runners = runners;
        this.access = access;
        this.availabilityStore = availabilityStore;
        this.environmentStore = environmentStore;
        this.timeProvider = timeProvider ?? TimeProvider.System;
        this.auditLogger = auditLogger;
    }

    private string AuditActor() => PrincipalDisplayName.Resolve(this.access.CurrentPrincipal) ?? "control-plane";

    /// <inheritdoc/>
    public async ValueTask<ListSchedulesResult> HandleListSchedulesAsync(ListSchedulesParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        AccessContext ctx = this.access.Current();
        int limit = parameters.Limit.IsNotUndefined() ? (int)parameters.Limit : 100;
        string? environment = parameters.Environment.IsNotUndefined() ? (string)parameters.Environment : null;

        // Naming the reserved $schedule id passes the visibility filter that hides schedule runs from ordinary run
        // queries — so the schedules surface (and only it) enumerates them, still reach-scoped by the store.
        JsonString pageToken = JsonString.From(parameters.PageToken);
        var query = new WorkflowQuery(WorkflowId: ScheduleWorkflowId, Limit: limit, ContinuationToken: pageToken);
        using WorkflowRunPage page = await this.management.ListAsync(query, ctx, cancellationToken).ConfigureAwait(false);

        // Load each scheduler run's state (reach-gated, bounded by the page size). The states are held ALIVE through the
        // synchronous response build below (each schedule's fields are value-bridged from its inputs), then disposed.
        var views = new List<ScheduleView>(page.Runs.Count);
        try
        {
            foreach (WorkflowRunListing listing in page.Runs)
            {
                WorkflowCheckpointState? state = await this.management.LoadStateAsync(listing.Id, ctx, cancellationToken).ConfigureAwait(false);
                if (state is null)
                {
                    continue;
                }

                if (!IsLiveSchedule(state) || (environment is not null && !string.Equals(state.Environment, environment, StringComparison.Ordinal)))
                {
                    state.Dispose();
                    continue;
                }

                views.Add(this.CreateView(state));
            }

            // Materialise the page NOW, while the states are alive, into an owned document handed to the workspace (it
            // disposes it after the response is written). The states are then disposed in the finally: the response no
            // longer references them.
            ReadOnlyMemory<byte> nextPageToken = page.NextPageToken;
            ParsedJsonDocument<Models.ScheduleList> doc = Models.ScheduleList.Create(
                in views,
                Models.ScheduleList.ScheduleArray.Build(in views, BuildScheduleArray),
                nextPageToken: nextPageToken.IsEmpty ? default : (Models.JsonString.Source)nextPageToken.Span);
            workspace.TakeOwnership(doc);
            return ListSchedulesResult.Ok(doc.RootElement, workspace);
        }
        finally
        {
            foreach (ScheduleView view in views)
            {
                view.State.Dispose();
            }
        }
    }

    /// <inheritdoc/>
    public async ValueTask<CreateScheduleResult> HandleCreateScheduleAsync(CreateScheduleParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        AccessContext ctx = this.access.Current();
        Models.ScheduleCreate body = parameters.Body;

        if (!body.ScheduleId.IsNotUndefined() || !body.Environment.IsNotUndefined() || !body.TargetBaseWorkflowId.IsNotUndefined()
            || !body.TargetVersionNumber.IsNotUndefined() || !body.Cron.IsNotUndefined())
        {
            return CreateScheduleResult.BadRequest(
                Problem("invalid-schedule", "Invalid schedule", 400, "scheduleId, environment, targetBaseWorkflowId, targetVersionNumber, and cron are required."), workspace);
        }

        // These are realised because the downstream seams take strings/ints: the idempotency key + run-id derivation,
        // the environment pin, the catalog lookup, and the Cronos parse. targetInputs stays JSON (value-bridged below).
        string scheduleId = (string)body.ScheduleId;
        string environment = (string)body.Environment;
        string targetBase = (string)body.TargetBaseWorkflowId;
        int targetVersion = (int)body.TargetVersionNumber;
        string cron = (string)body.Cron;
        string timeZone = body.TimeZone.IsNotUndefined() ? (string)body.TimeZone : "UTC";
        bool includeSeconds = body.IncludeSeconds.IsNotUndefined() && (bool)body.IncludeSeconds;

        try
        {
            _ = new CronSchedule(cron, TimeZoneInfo.FindSystemTimeZoneById(timeZone), includeSeconds);
        }
        catch (Exception ex) when (ex is FormatException or TimeZoneNotFoundException or InvalidTimeZoneException or ArgumentException)
        {
            return CreateScheduleResult.BadRequest(Problem("invalid-cron", "Invalid cadence", 400, ex.Message), workspace);
        }

        // The target version must exist, be in the caller's reach (non-disclosing 404), runnable, available in the
        // environment (§7.8), and hosted — the same gates an operator start passes, checked now so the schedule does not
        // fail silently at every occurrence.
        using ParsedJsonDocument<CatalogVersion>? version = await this.catalog.GetAsync(targetBase, targetVersion, ctx, cancellationToken).ConfigureAwait(false);
        if (version is not { } versionDoc)
        {
            return CreateScheduleResult.NotFound(
                Problem("target-not-found", "Target not found", 404, $"Version {targetVersion} of '{targetBase}' does not exist or is outside your reach."), workspace);
        }

        CatalogVersion catalogVersion = versionDoc.RootElement;
        if (!(bool)catalogVersion.Runnable)
        {
            return CreateScheduleResult.Conflict(
                Problem("not-runnable", "Target not runnable", 409, $"Version {targetVersion} of '{targetBase}' carries no compiled executor; it cannot be scheduled."), workspace);
        }

        if (this.availabilityStore is { } availStore)
        {
            using var availabilityEntry = await availStore.GetAsync(targetBase, targetVersion, environment, cancellationToken).ConfigureAwait(false);
            if (availabilityEntry is null)
            {
                return CreateScheduleResult.Conflict(
                    Problem("not-available", "Target not available in environment", 409, $"Version {targetVersion} of '{targetBase}' is not available in environment '{environment}' (§7.8); make it available and retry."), workspace);
            }
        }

        if (!await this.runners.IsVersionHostedAsync(targetBase, targetVersion, cancellationToken).ConfigureAwait(false))
        {
            return CreateScheduleResult.Conflict(
                Problem("no-runner", "No hosting runner", 409, $"No registered runner currently hosts version {targetVersion} of '{targetBase}'; a schedule needs one to run its target."), workspace);
        }

        // The distinguishing gate for a schedule (#896): the environment must have a runner that serves schedules.
        if (!await this.runners.IsSchedulingHostedAsync(environment, cancellationToken).ConfigureAwait(false))
        {
            return CreateScheduleResult.Conflict(
                Problem("no-scheduler", "No scheduling runner", 409, $"No runner serving environment '{environment}' advertises scheduling. Start (or configure) a runner there with servesSchedules enabled, then retry."), workspace);
        }

        // Build the scheduler run's inputs by the generated type's own typed construction (pooled, no hand-rolled JSON),
        // bridging the caller's targetInputs in as a JSON value. The versioned target id is the base + version.
        string targetWorkflowId = targetBase + "-v" + targetVersion.ToString(CultureInfo.InvariantCulture);
        using ParsedJsonDocument<WorkflowScheduleInput> scheduleInput = body.TargetInputs.IsNotUndefined()
            ? WorkflowScheduleInput.Create(scheduleId: scheduleId, cron: cron, targetWorkflowId: targetWorkflowId, timeZone: timeZone, includeSeconds: includeSeconds, targetInputs: (JsonElement)body.TargetInputs)
            : WorkflowScheduleInput.Create(scheduleId: scheduleId, cron: cron, targetWorkflowId: targetWorkflowId, timeZone: timeZone, includeSeconds: includeSeconds);
        await this.management.StartIdempotentAsync(
            ScheduleWorkflowId, (JsonElement)scheduleInput.RootElement, scheduleId, environment, securityTags: catalogVersion.SecurityTagsValue, cancellationToken: cancellationToken).ConfigureAwait(false);

        GovernanceAudit.Mutation(this.auditLogger, "schedule.create", this.AuditActor(), TargetKind, scheduleId, "created");

        WorkflowRunId runId = SecuredWorkflowManagement.IdempotentRunId(ScheduleWorkflowId, scheduleId);
        WorkflowCheckpointState? created = await this.management.LoadStateAsync(runId, ctx, cancellationToken).ConfigureAwait(false);
        if (created is null)
        {
            return CreateScheduleResult.NotFound(
                Problem("schedule-not-found", "Schedule not found", 404, "The schedule was created but could not be read back within your reach."), workspace);
        }

        try
        {
            ParsedJsonDocument<Models.Schedule> doc = BuildScheduleDocument(this.CreateView(created));
            workspace.TakeOwnership(doc);
            return CreateScheduleResult.Created(doc.RootElement, workspace);
        }
        finally
        {
            created.Dispose();
        }
    }

    /// <inheritdoc/>
    public async ValueTask<GetScheduleResult> HandleGetScheduleAsync(GetScheduleParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        AccessContext ctx = this.access.Current();
        string scheduleId = (string)parameters.ScheduleId;
        WorkflowRunId runId = SecuredWorkflowManagement.IdempotentRunId(ScheduleWorkflowId, scheduleId);
        WorkflowCheckpointState? state = await this.management.LoadStateAsync(runId, ctx, cancellationToken).ConfigureAwait(false);
        if (state is null || !IsLiveSchedule(state))
        {
            state?.Dispose();
            return GetScheduleResult.NotFound(NotFoundProblem(scheduleId), workspace);
        }

        try
        {
            ParsedJsonDocument<Models.Schedule> doc = BuildScheduleDocument(this.CreateView(state));
            workspace.TakeOwnership(doc);
            return GetScheduleResult.Ok(doc.RootElement, workspace);
        }
        finally
        {
            state.Dispose();
        }
    }

    /// <inheritdoc/>
    public async ValueTask<DeleteScheduleResult> HandleDeleteScheduleAsync(DeleteScheduleParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        AccessContext ctx = this.access.Current();
        string scheduleId = (string)parameters.ScheduleId;
        WorkflowRunId runId = SecuredWorkflowManagement.IdempotentRunId(ScheduleWorkflowId, scheduleId);

        // Confirm the run is actually a schedule (and in reach) before cancelling — a caller must not cancel an
        // arbitrary run by presenting an id that happens to hash into it.
        using (WorkflowCheckpointState? state = await this.management.LoadStateAsync(runId, ctx, cancellationToken).ConfigureAwait(false))
        {
            if (state is null || !IsLiveSchedule(state))
            {
                return DeleteScheduleResult.NotFound(NotFoundProblem(scheduleId), workspace);
            }
        }

        bool cancelled = await this.management.CancelAsync(runId, "Schedule cancelled via the schedules surface (#896).", ctx, cancellationToken).ConfigureAwait(false);
        if (!cancelled)
        {
            return DeleteScheduleResult.NotFound(NotFoundProblem(scheduleId), workspace);
        }

        GovernanceAudit.Mutation(this.auditLogger, "schedule.delete", this.AuditActor(), TargetKind, scheduleId, "cancelled");
        return DeleteScheduleResult.NoContent();
    }

    /// <inheritdoc/>
    public async ValueTask<RunScheduleNowResult> HandleRunScheduleNowAsync(RunScheduleNowParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        AccessContext ctx = this.access.Current();
        string scheduleId = (string)parameters.ScheduleId;
        WorkflowRunId scheduleRunId = SecuredWorkflowManagement.IdempotentRunId(ScheduleWorkflowId, scheduleId);

        // The scheduler run's state is held alive across the whole start: the target inputs are passed as a JSON value
        // straight from it to the start path (which copies them at persist), so no owned copy is realised here.
        using WorkflowCheckpointState? state = await this.management.LoadStateAsync(scheduleRunId, ctx, cancellationToken).ConfigureAwait(false);
        if (state is null || !IsLiveSchedule(state))
        {
            return RunScheduleNowResult.NotFound(NotFoundProblem(scheduleId), workspace);
        }

        WorkflowScheduleInput spec = WorkflowScheduleInput.From(state.Inputs);
        string targetWorkflowId = spec.TargetWorkflowIdValue;
        string environment = state.Environment ?? string.Empty;
        (string targetBase, int targetVersion) = ParseVersionedId(targetWorkflowId);

        using ParsedJsonDocument<CatalogVersion>? version = await this.catalog.GetAsync(targetBase, targetVersion, ctx, cancellationToken).ConfigureAwait(false);
        if (version is not { } versionDoc)
        {
            return RunScheduleNowResult.NotFound(
                Problem("target-not-found", "Target not found", 404, $"Version {targetVersion} of '{targetBase}' does not exist or is outside your reach."), workspace);
        }

        CatalogVersion catalogVersion = versionDoc.RootElement;
        if (!(bool)catalogVersion.Runnable)
        {
            return RunScheduleNowResult.Conflict(Problem("not-runnable", "Target not runnable", 409, $"Version {targetVersion} of '{targetBase}' carries no compiled executor."), workspace);
        }

        if (this.availabilityStore is { } availStore)
        {
            using var availabilityEntry = await availStore.GetAsync(targetBase, targetVersion, environment, cancellationToken).ConfigureAwait(false);
            if (availabilityEntry is null)
            {
                return RunScheduleNowResult.Conflict(Problem("not-available", "Target not available in environment", 409, $"Version {targetVersion} of '{targetBase}' is not available in environment '{environment}' (§7.8)."), workspace);
            }
        }

        if (!await this.runners.IsVersionHostedAsync(targetBase, targetVersion, cancellationToken).ConfigureAwait(false))
        {
            return RunScheduleNowResult.Conflict(Problem("no-runner", "No hosting runner", 409, $"No registered runner currently hosts version {targetVersion} of '{targetBase}'."), workspace);
        }

        JsonElement targetInputs = spec.TargetInputs.IsNotUndefined() ? (JsonElement)spec.TargetInputs : default;
        WorkflowRunId runId = await this.management.StartAsync(
            targetWorkflowId, targetInputs, correlationId: null, tags: default, securityTags: catalogVersion.SecurityTagsValue, environment: environment, cancellationToken).ConfigureAwait(false);

        GovernanceAudit.Mutation(this.auditLogger, "schedule.run-now", this.AuditActor(), TargetKind, scheduleId, "started");

        return RunScheduleNowResult.Accepted(
            new Models.WorkflowRunAccepted.Source((ref Models.WorkflowRunAccepted.Builder b) => b.Create(
                runId: runId.Value,
                status: WorkflowRunStatus.Pending.ToString(),
                workflowId: targetWorkflowId)),
            workspace);
    }

    // ── projection ─────────────────────────────────────────────────────────────────────────────────────────────────

    // Precompute the two values that need `this` (the clock) or a realised string (the Cronos parse) while we are still
    // async; the response build itself is a static, closure-free pass over the alive state.
    private ScheduleView CreateView(WorkflowCheckpointState state)
    {
        WorkflowScheduleInput spec = WorkflowScheduleInput.From(state.Inputs);
        DateTimeOffset? next = null;
        try
        {
            next = new CronSchedule(spec.CronValue, TimeZoneInfo.FindSystemTimeZoneById(spec.TimeZoneValue), spec.IncludeSecondsValue)
                .GetNextOccurrence(this.timeProvider.GetUtcNow());
        }
        catch (Exception ex) when (ex is FormatException or TimeZoneNotFoundException or InvalidTimeZoneException or ArgumentException)
        {
            // A schedule whose cadence no longer parses (a removed time zone) has no computable next occurrence.
        }

        return new ScheduleView(state, next, ReadWatermark(state));
    }

    private static DateTimeOffset? ReadWatermark(WorkflowCheckpointState state)
    {
        PooledUtf8Map<JsonElement>.Enumerator entries = state.StepOutputs.GetEnumerator();
        while (entries.MoveNext())
        {
            if (entries.CurrentKey.SequenceEqual("$watermark"u8) && entries.CurrentValue.TryGetDateTimeOffset(out DateTimeOffset watermark))
            {
                return watermark;
            }
        }

        return null;
    }

    private static void BuildScheduleArray(in List<ScheduleView> views, ref Models.ScheduleList.ScheduleArray.Builder array)
    {
        foreach (ScheduleView view in views)
        {
            array.AddItem(Models.Schedule.Build(in view, WriteSchedule));
        }
    }

    // The response fields: the pure-passthrough JSON strings are value-bridged bytes-to-bytes from the run's inputs; the
    // target id is realised once because it must be split into its base + version, the status is an enum, and the two
    // occurrences are computed instants.
    private static void WriteSchedule(in ScheduleView view, ref Models.Schedule.Builder b)
    {
        WorkflowScheduleInput spec = WorkflowScheduleInput.From(view.State.Inputs);
        string targetWorkflowId = spec.TargetWorkflowIdValue;
        (string targetBase, int targetVersion) = ParseVersionedId(targetWorkflowId);

        b.Create(
            scheduleId: Models.JsonString.From(spec.ScheduleId),
            environment: view.State.Environment is { } env ? (Models.JsonString.Source)env : default,
            targetBaseWorkflowId: targetBase,
            targetVersionNumber: targetVersion,
            targetWorkflowId: targetWorkflowId,
            cron: Models.JsonString.From(spec.Cron),
            timeZone: Models.JsonString.From(spec.TimeZone),
            includeSeconds: spec.IncludeSecondsValue,
            status: view.State.Status.ToString(),
            createdAt: view.State.CreatedAt,
            nextOccurrence: view.NextOccurrence is { } next ? (Models.JsonDateTime.Source)next : default,
            lastFiredOccurrence: view.LastFired is { } last ? (Models.JsonDateTime.Source)last : default);
    }

    // The single-schedule response, materialised NOW (the value bridges resolve while the state is alive) into an owned
    // document. The array form (list) resolves the same bridges through WriteSchedule when ScheduleList.Create builds.
    private static ParsedJsonDocument<Models.Schedule> BuildScheduleDocument(in ScheduleView view)
    {
        WorkflowScheduleInput spec = WorkflowScheduleInput.From(view.State.Inputs);
        string targetWorkflowId = spec.TargetWorkflowIdValue;
        (string targetBase, int targetVersion) = ParseVersionedId(targetWorkflowId);

        return Models.Schedule.Create(
            scheduleId: Models.JsonString.From(spec.ScheduleId),
            environment: view.State.Environment is { } env ? (Models.JsonString.Source)env : default,
            targetBaseWorkflowId: targetBase,
            targetVersionNumber: targetVersion,
            targetWorkflowId: targetWorkflowId,
            cron: Models.JsonString.From(spec.Cron),
            timeZone: Models.JsonString.From(spec.TimeZone),
            includeSeconds: spec.IncludeSecondsValue,
            status: view.State.Status.ToString(),
            createdAt: view.State.CreatedAt,
            nextOccurrence: view.NextOccurrence is { } next ? (Models.JsonDateTime.Source)next : default,
            lastFiredOccurrence: view.LastFired is { } last ? (Models.JsonDateTime.Source)last : default);
    }

    // A schedule is "present" only while its scheduler run is live. Delete cancels the run and completion (never reached
    // by the perpetual loop in normal operation) leaves a terminal run in the store; both read back as absent — 404 from
    // get/delete/run-now, omitted from the list. A faulted schedule stays visible so an operator can see the breakage.
    private static bool IsLiveSchedule(WorkflowCheckpointState state)
        => string.Equals(state.WorkflowId, ScheduleWorkflowId, StringComparison.Ordinal)
           && state.Status is not (WorkflowRunStatus.Cancelled or WorkflowRunStatus.Completed);

    // ── helpers ────────────────────────────────────────────────────────────────────────────────────────────────────
    private static (string BaseWorkflowId, int VersionNumber) ParseVersionedId(string workflowId)
    {
        int suffix = workflowId.LastIndexOf("-v", StringComparison.Ordinal);
        if (suffix > 0 && int.TryParse(workflowId.AsSpan(suffix + 2), NumberStyles.None, CultureInfo.InvariantCulture, out int version))
        {
            return (workflowId[..suffix], version);
        }

        return (workflowId, 0);
    }

    private static Models.ProblemDetails.Source NotFoundProblem(string scheduleId)
        => Problem("schedule-not-found", "Schedule not found", 404, $"No schedule '{scheduleId}' exists, or it is outside your reach.");

    private static Models.ProblemDetails.Source Problem(string type, string title, int status, string detail)
        => new((ref Models.ProblemDetails.Builder b) => b.Create(
            detail: detail,
            status: status,
            title: title,
            type: ProblemBase + type));

    // A loaded scheduler run plus the two derived values that need async/realised context, threaded by `in` into the
    // static response build. The State is disposed by the caller after the (synchronous) build resolves. This is a build
    // CONTEXT (like the sources handler's tag-span/management-tag carriers), not a persisted or API shape — those are the
    // generated Models.Schedule / WorkflowScheduleInput.
    private readonly record struct ScheduleView(WorkflowCheckpointState State, DateTimeOffset? NextOccurrence, DateTimeOffset? LastFired);
}