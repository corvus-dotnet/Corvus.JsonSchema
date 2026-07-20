// <copyright file="ScheduleHostedWorkflow.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using Corvus.Text.Json;
using Corvus.Text.Json.AsyncApi;
using Corvus.Text.Json.OpenApi;

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// The built-in scheduler workflow (#896): a durable schedule is a durable <em>run</em> of this workflow. On each
/// entry it fires every occurrence of its cadence that is now due (starting a run of the target workflow through
/// the host start path, idempotently), advances and checkpoints its watermark, then suspends on a durable timer
/// until the next occurrence. Because a schedule is an ordinary durable run, it rides the existing wait index,
/// CAS+lease dispatch, and environment pinning, and survives runner recycling, with no new per-backend store: a
/// runner that was down while an occurrence came due fires it late on resume rather than missing it.
/// </summary>
/// <remarks>
/// This is a hand-written <see cref="IHostedWorkflow"/> (the contract is deliberately host-implementable), not a
/// generated executor, because its "step" is cron-next evaluation, which the workflow language does not express.
/// The host resumer routes runs whose <see cref="WorkflowDescriptor.WorkflowId"/> is <see cref="ScheduleWorkflowId"/>
/// here; those runs are a distinct <em>kind</em> that operator run queries filter out.
/// </remarks>
public sealed class ScheduleHostedWorkflow : IHostedWorkflow
{
    /// <summary>The reserved workflow id every schedule run carries, so the host resumer routes it here and
    /// operator run queries can exclude schedules as a distinct kind.</summary>
    public const string ScheduleWorkflowId = "$schedule";

    // The step id the watermark (the last occurrence fired) is checkpointed under, so it is restored on resume.
    private const string WatermarkStep = "$watermark";

    private readonly WorkflowStartHandler start;
    private readonly TimeProvider timeProvider;
    private readonly Func<WorkflowScheduleInput, ISchedule> scheduleFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="ScheduleHostedWorkflow"/> class.
    /// </summary>
    /// <param name="start">The host start path each occurrence fires the target workflow through.</param>
    /// <param name="timeProvider">The clock for cadence evaluation and timer delays.</param>
    /// <param name="scheduleFactory">Builds the cadence from a schedule's inputs; defaults to a <see cref="CronSchedule"/>.</param>
    public ScheduleHostedWorkflow(WorkflowStartHandler start, TimeProvider timeProvider, Func<WorkflowScheduleInput, ISchedule>? scheduleFactory = null)
    {
        ArgumentNullException.ThrowIfNull(start);
        ArgumentNullException.ThrowIfNull(timeProvider);
        this.start = start;
        this.timeProvider = timeProvider;
        this.scheduleFactory = scheduleFactory ?? DefaultSchedule;
    }

    /// <inheritdoc/>
    public WorkflowDescriptor Descriptor => new(ScheduleWorkflowId, NeedsMessageTransport: false, Sources: []);

    /// <inheritdoc/>
    public async ValueTask<WorkflowRunResultKind> RunAsync(
        IReadOnlyDictionary<string, IApiTransport> apiTransports,
        IMessageTransport? messageTransport,
        JsonWorkspace workspace,
        JsonElement inputs,
        IWorkflowRun run,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);

        WorkflowScheduleInput spec = WorkflowScheduleInput.From(inputs);
        ISchedule schedule = this.scheduleFactory(spec);
        string scheduleId = spec.ScheduleIdValue;
        string targetWorkflowId = spec.TargetWorkflowIdValue;
        JsonElement targetInputs = spec.TargetInputs.IsNotUndefined() ? (JsonElement)spec.TargetInputs : default;

        // Restore the watermark (the last occurrence fired). A fresh run seeds it to now, so occurrences before the
        // schedule was registered are not fired retroactively; a resumed run restores the persisted watermark, so
        // occurrences that came due while no runner was advancing it are caught up rather than skipped.
        DateTimeOffset now = this.timeProvider.GetUtcNow();
        DateTimeOffset watermark = now;
        if (run.TryGetStepOutputs(WatermarkStep, out JsonElement stored)
            && stored.ValueKind == JsonValueKind.String
            && DateTimeOffset.TryParse(stored.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTimeOffset restored))
        {
            watermark = restored;
        }

        // Fire every occurrence due as of now, advancing the watermark. Each fire is idempotent on
        // scheduleId + occurrence, so a duplicated resume does not start the target twice.
        while (schedule.GetNextOccurrence(watermark) is { } occurrence && occurrence <= now)
        {
            string idempotencyKey = scheduleId + ":" + occurrence.ToString("O", CultureInfo.InvariantCulture);
            await this.start(new WorkflowStartRequest(targetWorkflowId, targetInputs, idempotencyKey), cancellationToken).ConfigureAwait(false);
            watermark = occurrence;
        }

        // Persist the watermark and suspend until the next occurrence. A cadence with no further occurrences
        // (a bounded cron) completes instead of suspending. The parsed document stays alive through the
        // checkpoint that SuspendForTimerAsync / CompleteAsync writes.
        using ParsedJsonDocument<JsonElement> watermarkDoc =
            ParsedJsonDocument<JsonElement>.Parse("\"" + watermark.ToString("O", CultureInfo.InvariantCulture) + "\"");
        run.SetStepOutputs(WatermarkStep, watermarkDoc.RootElement);

        if (schedule.GetNextOccurrence(watermark) is { } next)
        {
            TimeSpan delay = next - this.timeProvider.GetUtcNow();
            await run.SuspendForTimerAsync(cursor: 0, delay > TimeSpan.Zero ? delay : TimeSpan.Zero, cancellationToken).ConfigureAwait(false);
            return WorkflowRunResultKind.Suspended;
        }

        await run.CompleteAsync(default, cancellationToken).ConfigureAwait(false);
        return WorkflowRunResultKind.Completed;
    }

    private static ISchedule DefaultSchedule(WorkflowScheduleInput spec)
    {
        TimeZoneInfo timeZone = TimeZoneInfo.FindSystemTimeZoneById(spec.TimeZoneValue);
        return new CronSchedule(spec.CronValue, timeZone, spec.IncludeSecondsValue);
    }
}