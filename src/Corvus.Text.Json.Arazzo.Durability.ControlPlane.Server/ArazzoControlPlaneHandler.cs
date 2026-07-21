// <copyright file="ArazzoControlPlaneHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo;
using Corvus.Text.Json.Arazzo.Durability;
using Microsoft.Extensions.Logging;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server;

/// <summary>
/// Implements the generated <see cref="IApiRunsHandler"/> over an <see cref="ISecuredWorkflowManagement"/>,
/// mapping each control-plane REST operation onto the corresponding management-client call and projecting the
/// .NET result DTOs into the generated response models.
/// </summary>
public sealed class ArazzoControlPlaneHandler : IApiRunsHandler
{
    private const string ProblemBase = "https://corvus-oss.org/arazzo/control-plane/problems/";

    // Bounds the runs count so a busy list renders "100+" rather than paying to count an unbounded set (count-API §16.5).
    private const int CountCap = 100;

    private readonly ISecuredWorkflowManagement management;
    private readonly ControlPlaneAccess access;
    private readonly ISecuredWorkflowCatalog? catalog;
    private readonly ILogger? auditLogger;

    /// <summary>Initializes a new instance of the <see cref="ArazzoControlPlaneHandler"/> class (unscoped: full access).</summary>
    /// <param name="management">The control-plane client the endpoints delegate to.</param>
    public ArazzoControlPlaneHandler(ISecuredWorkflowManagement management)
        : this(management, new ControlPlaneAccess())
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ArazzoControlPlaneHandler"/> class.</summary>
    /// <param name="management">The control-plane client the endpoints delegate to.</param>
    /// <param name="access">Resolves the caller's <see cref="AccessContext"/> per request (§14.2).</param>
    /// <param name="catalog">The catalog, used to resolve a run's version output-sensitivity classification for the §14
    /// step-journal disclosure tier; when <see langword="null"/>, version-level journal redaction is not applied (the
    /// <c>runs:outputs:read</c> baseline gate and field-level redaction still apply). The full-access ctor never needs it:
    /// an unscoped caller always holds the stronger grant, so the classification is never consulted.</param>
    /// <param name="auditLogger">The logger for the §860 step-journal read-access audit (who read which run's journal, at
    /// which disclosure tier); when <see langword="null"/>, only the audit span is emitted (no structured log). The read
    /// audit is emitted regardless of this — the span rides the always-registered <see cref="ArazzoTelemetry.ActivitySource"/>.</param>
    internal ArazzoControlPlaneHandler(ISecuredWorkflowManagement management, ControlPlaneAccess access, ISecuredWorkflowCatalog? catalog = null, ILogger? auditLogger = null)
    {
        ArgumentNullException.ThrowIfNull(management);
        ArgumentNullException.ThrowIfNull(access);
        this.management = management;
        this.access = access;
        this.catalog = catalog;
        this.auditLogger = auditLogger;
    }

    // The audited resource kind for a run mutation (design §850, worklist item 7).
    private const string RunTargetKind = "run";

    // The §850 audit subject for a run mutation: the authenticated caller (the operator who decided it), not the fixed
    // lease-owner identity the domain service's execution span carries. Falls back to "system" when unresolved.
    private string AuditActor() => PrincipalDisplayName.Resolve(this.access.CurrentPrincipal) ?? "system";

    /// <inheritdoc/>
    public async ValueTask<ListRunsResult> HandleListRunsAsync(ListRunsParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        WorkflowRunStatus? status = parameters.Status.IsNotUndefined() ? Enum.Parse<WorkflowRunStatus>((string)parameters.Status) : null;
        string? workflowId = parameters.WorkflowId.IsNotUndefined() ? (string)parameters.WorkflowId : null;
        int limit = parameters.Limit.IsNotUndefined() ? (int)parameters.Limit : 100;

        // The opaque page token flows to the store as its JSON value (From() rewraps parameters.PageToken — free, no
        // reify, no managed string; an undefined token rewraps to an undefined JsonString the store reads as "first
        // page"); the store decodes the run-id cursor bytes-native from the request UTF-8.
        JsonString pageToken = JsonString.From(parameters.PageToken);
        DateTimeOffset? createdAfter = ParseInstant(parameters.CreatedAfter);
        DateTimeOffset? createdBefore = ParseInstant(parameters.CreatedBefore);
        DateTimeOffset? updatedAfter = ParseInstant(parameters.UpdatedAfter);
        DateTimeOffset? updatedBefore = ParseInstant(parameters.UpdatedBefore);
        string? correlationId = parameters.CorrelationId.IsNotUndefined() ? (string)parameters.CorrelationId : null;
        TagSet tags = ParseTags(parameters.Tag);

        // `using`: the page owns the pooled continuation-token buffer; BuildPage's Source closure copies it into the
        // response workspace synchronously inside Ok (CreateBuilder materialises eagerly), so disposing after Ok is safe.
        using WorkflowRunPage page = await this.management.ListAsync(
            new WorkflowQuery(status, workflowId, limit, pageToken, createdAfter, createdBefore, updatedAfter, updatedBefore, correlationId, tags),
            this.access.Current(),
            cancellationToken).ConfigureAwait(false);
        return ListRunsResult.Ok(BuildPage(page), workspace);
    }

    /// <inheritdoc/>
    public async ValueTask<CountRunsResult> HandleCountRunsAsync(CountRunsParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        // Same visibility filters and §14.2 read reach as HandleListRunsAsync, but the store returns only a bounded
        // total, never rows (work badges + list footers). CountCap bounds it so a busy list renders "100+".
        WorkflowRunStatus? status = parameters.Status.IsNotUndefined() ? Enum.Parse<WorkflowRunStatus>((string)parameters.Status) : null;
        string? workflowId = parameters.WorkflowId.IsNotUndefined() ? (string)parameters.WorkflowId : null;
        DateTimeOffset? createdAfter = ParseInstant(parameters.CreatedAfter);
        DateTimeOffset? createdBefore = ParseInstant(parameters.CreatedBefore);
        DateTimeOffset? updatedAfter = ParseInstant(parameters.UpdatedAfter);
        DateTimeOffset? updatedBefore = ParseInstant(parameters.UpdatedBefore);
        string? correlationId = parameters.CorrelationId.IsNotUndefined() ? (string)parameters.CorrelationId : null;
        TagSet tags = ParseTags(parameters.Tag);

        // The query's limit / page token are irrelevant to a count; CountAsync counts the whole matching set bounded
        // by CountCap. Reach is applied inside CountAsync (context.Reach(Read)), reusing the list's predicate.
        (int count, bool capped) = await this.management.CountAsync(
            new WorkflowQuery(status, workflowId, CountCap, default, createdAfter, createdBefore, updatedAfter, updatedBefore, correlationId, tags),
            this.access.Current(),
            CountCap,
            cancellationToken).ConfigureAwait(false);
        return CountRunsResult.Ok(Models.CountResult.Build(capped: capped, count: count), workspace);
    }

    private static DateTimeOffset? ParseInstant(Models.JsonDateTime value)
        => value.IsNotUndefined()
            ? DateTimeOffset.Parse((string)value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
            : null;

    // The query needle: copy the parsed tag-list parameter's canonical bytes into the holder (per request, not per
    // row). The in-memory backends match it span-wise; the SQL/Cosmos/Mongo backends bind it at their parameter leaf.
    private static TagSet ParseTags(Models.TagList tag) => TagSet.CopyFrom(tag);

    /// <inheritdoc/>
    public async ValueTask<GetRunResult> HandleGetRunAsync(GetRunParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string runId = (string)parameters.RunId;

        // The read is gated by the caller's read reach (§14.2): a run outside it comes back null and is reported
        // as 404 (non-disclosing).
        WorkflowRunDetail? detail = await this.management.GetAsync(runId, this.access.Current(), cancellationToken).ConfigureAwait(false);
        return detail is { } d
            ? GetRunResult.Ok(BuildDetail(d), workspace)
            : GetRunResult.NotFound(NotFoundProblem(runId), workspace);
    }

    /// <inheritdoc/>
    public async ValueTask<GetRunStepsResult> HandleGetRunStepsAsync(GetRunStepsParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string runId = (string)parameters.RunId;
        AccessContext ctx = this.access.Current();

        // §860: reading a run's step journal (a sensitive-payload read, §14) is audited — who read which run, at which
        // disclosure tier — so a sensitive read no longer leaves no trace. The caller is the audited subject.
        string actor = PrincipalDisplayName.Resolve(this.access.CurrentPrincipal) ?? "system";

        // The journal discloses strictly more than the detail, so resolve the run first (for its version + reach): out of
        // read reach or absent → 404 (non-disclosing), the same gate GetStepJournalAsync applies.
        WorkflowRunDetail? detail = await this.management.GetAsync(runId, ctx, cancellationToken).ConfigureAwait(false);
        if (detail is not { } d)
        {
            // A refused read (out of reach or absent) is audited too — it is an attempted-access signal.
            SensitiveReadAudit.JournalRead(this.auditLogger, actor, runId, string.Empty, JournalDisclosure.Refused);
            return GetRunStepsResult.NotFound(NotFoundProblem(runId), workspace);
        }

        ReadOnlyMemory<byte>? journal = await this.management.GetStepJournalAsync(runId, ctx, cancellationToken).ConfigureAwait(false);
        if (journal is not { } bytes)
        {
            SensitiveReadAudit.JournalRead(this.auditLogger, actor, runId, d.WorkflowId, JournalDisclosure.Refused);
            return GetRunStepsResult.NotFound(NotFoundProblem(runId), workspace);
        }

        // §14 disclosure tier: the endpoint has already demanded the runs:outputs:read baseline scope. On top of that, a
        // version its author classified sensitive has its WHOLE step journal redacted for callers below the stronger grant
        // — write reach on the run (operator-level, §14.2). The classification is read from the CURRENT catalog version, so
        // reclassifying a workflow retroactively protects existing runs' journals.
        bool strongerGrant = ctx.Admits(AccessVerb.Write, d.SecurityTags);
        bool redacted = !strongerGrant && await this.IsOutputsSensitiveVersionAsync(d.WorkflowId, cancellationToken).ConfigureAwait(false);
        if (redacted)
        {
            bytes = RedactAllOutputs(bytes);
        }

        // §860 read-access audit: who read this run's step journal, and whether the payloads were disclosed or withheld.
        SensitiveReadAudit.JournalRead(this.auditLogger, actor, runId, d.WorkflowId, redacted ? JournalDisclosure.Redacted : JournalDisclosure.Full);

        // Hand the parsed journal to the response workspace so it lives until the response is written
        // (the result Body references it); the workspace disposes it — do not dispose it here.
        ParsedJsonDocument<Models.WorkflowRunSteps> parsed = ParsedJsonDocument<Models.WorkflowRunSteps>.Parse(bytes);
        workspace.TakeOwnership(parsed);
        return GetRunStepsResult.Ok(parsed.RootElement, workspace);
    }

    // Whether the run's catalog version is classified output-sensitive (§14). Read trusted (System) — the classification
    // is a governance property, not reach-gated data, so a caller who may read the run always sees the correct tier
    // (never fail-open because the version happens to sit outside their reach). Absent catalog or an unparseable/unknown
    // version → not sensitive (the baseline scope and field-level redaction still apply).
    private async ValueTask<bool> IsOutputsSensitiveVersionAsync(string workflowId, CancellationToken cancellationToken)
    {
        if (this.catalog is null || !TryParseVersionedId(workflowId, out string baseWorkflowId, out int versionNumber))
        {
            return false;
        }

        using ParsedJsonDocument<CatalogVersion>? version = await this.catalog.GetAsync(baseWorkflowId, versionNumber, AccessContext.System, cancellationToken).ConfigureAwait(false);
        return version is { } v && v.RootElement.IsOutputsSensitive;
    }

    // Split a versioned workflow id ("{base}-v{n}") into its base id and version number (mirrors HostedWorkflowResumer).
    private static bool TryParseVersionedId(string workflowId, out string baseWorkflowId, out int versionNumber)
    {
        int suffix = workflowId.LastIndexOf("-v", StringComparison.Ordinal);
        if (suffix > 0 && int.TryParse(workflowId.AsSpan(suffix + 2), out versionNumber))
        {
            baseWorkflowId = workflowId[..suffix];
            return true;
        }

        baseWorkflowId = string.Empty;
        versionNumber = 0;
        return false;
    }

    // Rewrite the step journal withholding every step's outputs: each record keeps its stepId and is marked redacted, the
    // payload absent (non-disclosing). The whole-journal redaction for a version an author classified sensitive (§14).
    private static ReadOnlyMemory<byte> RedactAllOutputs(ReadOnlyMemory<byte> journalUtf8)
    {
        // Blank only the sensitive payload: copy every property except each step's `outputs`, and mark the step
        // redacted. The per-step status, attempt, and timing (ADR 0050) are not sensitive and are preserved, and
        // redaction stays decoupled from the journal schema (a new non-payload field survives unchanged here).
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(journalUtf8);
        JsonElement journal = doc.RootElement;

        var buffer = new System.Buffers.ArrayBufferWriter<byte>();
        using (var writer = new System.Text.Json.Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            foreach (JsonProperty<JsonElement> property in journal.EnumerateObject())
            {
                if (property.NameEquals("steps"u8))
                {
                    continue;
                }

                property.WriteTo(writer);
            }

            writer.WriteStartArray("steps"u8);
            if (journal.TryGetProperty("steps"u8, out JsonElement steps) && steps.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement step in steps.EnumerateArray())
                {
                    writer.WriteStartObject();
                    foreach (JsonProperty<JsonElement> field in step.EnumerateObject())
                    {
                        if (field.NameEquals("outputs"u8) || field.NameEquals("redacted"u8))
                        {
                            continue;
                        }

                        field.WriteTo(writer);
                    }

                    writer.WriteBoolean("redacted"u8, true);
                    writer.WriteEndObject();
                }
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return buffer.WrittenMemory;
    }

    /// <inheritdoc/>
    public async ValueTask<DeleteRunResult> HandleDeleteRunAsync(DeleteRunParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string runId = (string)parameters.RunId;
        AccessContext ctx = this.access.Current();

        // Gate before mutating (§14.2): a run outside read reach is 404 (non-disclosing); readable but outside
        // write reach is 403 (existence already disclosed by read).
        WorkflowRunDetail? detail = await this.management.GetAsync(runId, ctx, cancellationToken).ConfigureAwait(false);
        if (detail is not { } d)
        {
            return DeleteRunResult.NotFound(NotFoundProblem(runId), workspace);
        }

        if (!ctx.Admits(AccessVerb.Write, d.SecurityTags))
        {
            return DeleteRunResult.Forbidden(ForbiddenProblem(runId), workspace);
        }

        if (await this.management.DeleteAsync(runId, ctx, cancellationToken).ConfigureAwait(false))
        {
            GovernanceAudit.Mutation(this.auditLogger, "run.delete", this.AuditActor(), RunTargetKind, runId, "deleted");
            return DeleteRunResult.NoContent();
        }

        return DeleteRunResult.Conflict(Problem("not-deletable", "Run is not deletable", 409, $"Run '{runId}' is held by another owner; retry."), workspace);
    }

    /// <inheritdoc/>
    public async ValueTask<ResumeRunResult> HandleResumeRunAsync(ResumeRunParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string runId = (string)parameters.RunId;
        AccessContext ctx = this.access.Current();

        // Gate before mutating (§14.2): outside read reach → 404 (non-disclosing); readable but outside write
        // reach → 403.
        WorkflowRunDetail? before = await this.management.GetAsync(runId, ctx, cancellationToken).ConfigureAwait(false);
        if (before is not { } pre)
        {
            return ResumeRunResult.NotFound(NotFoundProblem(runId), workspace);
        }

        if (!ctx.Admits(AccessVerb.Write, pre.SecurityTags))
        {
            return ResumeRunResult.Forbidden(ForbiddenProblem(runId), workspace);
        }

        ResumeOptions options = ToResumeOptions(parameters.Body);
        if (await this.management.ResumeAsync(runId, options, ctx, cancellationToken).ConfigureAwait(false))
        {
            GovernanceAudit.Mutation(this.auditLogger, "run.resume", this.AuditActor(), RunTargetKind, runId, "resumed");
            WorkflowRunDetail? resumed = await this.management.GetAsync(runId, ctx, cancellationToken).ConfigureAwait(false);
            return resumed is { } d
                ? ResumeRunResult.Ok(BuildDetail(d), workspace)
                : ResumeRunResult.NotFound(NotFoundProblem(runId), workspace);
        }

        WorkflowRunDetail? current = await this.management.GetAsync(runId, ctx, cancellationToken).ConfigureAwait(false);
        return current is { } existing
            ? ResumeRunResult.Conflict(Problem("not-resumable", "Run is not resumable", 409, $"Run '{runId}' is {existing.Status}; only a Faulted run can be resumed (it may also be held by another owner)."), workspace)
            : ResumeRunResult.NotFound(NotFoundProblem(runId), workspace);
    }

    /// <inheritdoc/>
    public async ValueTask<CancelRunResult> HandleCancelRunAsync(CancelRunParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string runId = (string)parameters.RunId;
        AccessContext ctx = this.access.Current();

        // Gate before mutating (§14.2, see HandleResumeRunAsync): outside read reach → 404; readable but outside
        // write reach → 403.
        WorkflowRunDetail? before = await this.management.GetAsync(runId, ctx, cancellationToken).ConfigureAwait(false);
        if (before is not { } pre)
        {
            return CancelRunResult.NotFound(NotFoundProblem(runId), workspace);
        }

        if (!ctx.Admits(AccessVerb.Write, pre.SecurityTags))
        {
            return CancelRunResult.Forbidden(ForbiddenProblem(runId), workspace);
        }

        string reason = (string)parameters.Body.Reason;
        if (await this.management.CancelAsync(runId, reason, ctx, cancellationToken).ConfigureAwait(false))
        {
            GovernanceAudit.Mutation(this.auditLogger, "run.cancel", this.AuditActor(), RunTargetKind, runId, "cancelled");
            WorkflowRunDetail? cancelled = await this.management.GetAsync(runId, ctx, cancellationToken).ConfigureAwait(false);
            return cancelled is { } d
                ? CancelRunResult.Ok(BuildDetail(d), workspace)
                : CancelRunResult.NotFound(NotFoundProblem(runId), workspace);
        }

        WorkflowRunDetail? current = await this.management.GetAsync(runId, ctx, cancellationToken).ConfigureAwait(false);
        return current is { } existing
            ? CancelRunResult.Conflict(Problem("not-cancellable", "Run is not cancellable", 409, $"Run '{runId}' is {existing.Status}; a terminal run cannot be cancelled (it may also be held by another owner)."), workspace)
            : CancelRunResult.NotFound(NotFoundProblem(runId), workspace);
    }

    /// <inheritdoc/>
    public async ValueTask<PurgeRunsResult> HandlePurgeRunsAsync(PurgeRunsParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        // Purge is row-scoped by the caller's purge reach (§14.2), independent of the runs:purge capability scope
        // (§14.1): a tenant admin purges only their tenant's terminal runs, a service operator purges across tenants.
        var olderThan = DateTimeOffset.Parse((string)parameters.OlderThan, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        int limit = parameters.Limit.IsNotUndefined() ? (int)parameters.Limit : 1000;
        int purged = await this.management.PurgeAsync(new WorkflowPurgeQuery(olderThan, limit), this.access.Current(), cancellationToken).ConfigureAwait(false);

        // The bulk purge is audited by its cutoff (the count rides the workflow.purge execution span's purged_count tag).
        GovernanceAudit.Mutation(this.auditLogger, "run.purge", this.AuditActor(), RunTargetKind, $"olderThan={olderThan.ToString("O", CultureInfo.InvariantCulture)}", purged > 0 ? "purged" : "purged-none");
        return PurgeRunsResult.Ok(
            new Models.PurgeResult.Source((ref Models.PurgeResult.Builder b) => b.Create(purgedCount: purged)),
            workspace);
    }

    // Map the generated ResumeRequest union onto the engine's ResumeOptions by matching the variant the
    // `mode` const selected. An absent body (the request body is optional) means a plain retry.
    // Internal: the workspace handler's §18 debug runs apply the SAME union (§15 8b) — one mapping.
    internal static ResumeOptions ToResumeOptions(in Models.ResumeRequest body)
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

            // Emit the tags array straight from the persisted bytes — one detached parse for the response model, no
            // per-tag string materialization.
            Models.WorkflowRunDetail.JsonStringArray.Source tags = default;
            if (!d.Tags.IsEmpty)
            {
                tags = Models.WorkflowRunDetail.JsonStringArray.ParseValue(d.Tags.RawJson);
            }

            Models.JsonString.Source environment = default;
            if (d.Environment is { } env)
            {
                environment = env;
            }

            b.Create(
                createdAt: d.CreatedAt,
                cursor: d.Cursor,
                etag: d.Etag.Value ?? string.Empty,
                id: d.Id.Value,
                status: d.Status.ToString(),
                workflowId: d.WorkflowId,
                correlationId: correlationId,
                environment: environment,
                fault: fault,
                tags: tags,
                updatedAt: d.UpdatedAt is { } updated ? (Models.JsonDateTime.Source)updated : default,
                wait: wait);
        });

    private static Models.WorkflowRunPage.Source BuildPage(WorkflowRunPage page)
        => new((ref Models.WorkflowRunPage.Builder b) =>
        {
            // The token is the page's pooled UTF-8 (alive through this synchronous build); write it straight into the
            // response — no managed token string. Empty means "last page". (.Span is taken here, inside the closure
            // CreateBuilder runs synchronously during Ok, while the page is still owned by the handler.)
            b.Create(
                runs: new Models.WorkflowRunPage.WorkflowRunSummaryArray.Source(
                    (ref Models.WorkflowRunPage.WorkflowRunSummaryArray.Builder ab) =>
                    {
                        foreach (WorkflowRunListing listing in page.Runs)
                        {
                            ab.AddItem(BuildSummary(listing));
                        }
                    }),
                nextPageToken: page.NextPageToken.IsEmpty ? default : (Models.JsonString.Source)page.NextPageToken.Span);
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
            if (!e.Tags.IsEmpty)
            {
                tags = Models.WorkflowRunSummary.JsonStringArray.ParseValue(e.Tags.RawJson);
            }

            Models.JsonString.Source environment = default;
            if (e.Environment is { } env)
            {
                environment = env;
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
                environment: environment,
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

    private static Models.ProblemDetails.Source ForbiddenProblem(string runId)
        => Problem("forbidden", "Action not permitted", 403, $"You do not have permission to modify run '{runId}'.");

    private static Models.ProblemDetails.Source Problem(string type, string title, int status, string detail)
        => new((ref Models.ProblemDetails.Builder b) => b.Create(
            detail: detail,
            status: status,
            title: title,
            type: ProblemBase + type));
}