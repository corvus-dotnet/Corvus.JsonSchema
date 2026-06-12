// <copyright file="ScheduleWorkflowTrigger.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using Corvus.Text.Json;

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// The host configuration that binds a workflow to a schedule: each occurrence starts a run of
/// <see cref="WorkflowId"/>. The schedule cadence is supplied separately (an injected <see cref="ISchedule"/>),
/// so this binding stays plain, serializable host-config data — the schedule and the inputs are host config,
/// not baked into the Arazzo document.
/// </summary>
/// <param name="WorkflowId">The versioned workflow id (<c>{base}-v{n}</c>) to start.</param>
/// <param name="Inputs">The inputs each occurrence's run starts with; <see langword="default"/> starts the run with no inputs.</param>
public sealed record ScheduleTriggerBinding(
    string WorkflowId,
    JsonElement Inputs = default);

/// <summary>
/// A <see cref="IWorkflowTrigger"/> that starts a workflow run on a schedule. The occurrence instant is used as
/// the idempotency key, so a duplicate or retried fire for the same slot does not start a duplicate run.
/// </summary>
public sealed class ScheduleWorkflowTrigger : IWorkflowTrigger
{
    private readonly WorkflowStartHandler start;
    private readonly ScheduleTriggerBinding binding;
    private readonly ISchedule schedule;
    private readonly TimeProvider timeProvider;

    // The last occurrence fired (the watermark); the next tick fires every occurrence strictly after it and at or
    // before "now", so a host that was slow or asleep catches up rather than skipping slots. Seeded to the
    // construction instant so occurrences before the trigger existed are not retroactively fired.
    private DateTimeOffset cursor;

    private CancellationTokenSource? cts;
    private Task? loop;

    /// <summary>Initializes a new instance of the <see cref="ScheduleWorkflowTrigger"/> class.</summary>
    /// <param name="start">The host start path each occurrence invokes.</param>
    /// <param name="binding">The workflow + inputs binding.</param>
    /// <param name="schedule">The cadence driving the occurrences.</param>
    /// <param name="timeProvider">The time source for the schedule clock and waits.</param>
    public ScheduleWorkflowTrigger(WorkflowStartHandler start, ScheduleTriggerBinding binding, ISchedule schedule, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(start);
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentNullException.ThrowIfNull(schedule);
        ArgumentNullException.ThrowIfNull(timeProvider);
        this.start = start;
        this.binding = binding;
        this.schedule = schedule;
        this.timeProvider = timeProvider;
        this.cursor = timeProvider.GetUtcNow();
    }

    /// <inheritdoc/>
    public ValueTask StartListeningAsync(CancellationToken cancellationToken)
    {
        this.cursor = this.timeProvider.GetUtcNow();
        this.cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        this.loop = this.RunLoopAsync(this.cts.Token);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (this.cts is { } source)
        {
            await source.CancelAsync().ConfigureAwait(false);
        }

        if (this.loop is { } running)
        {
            try
            {
                await running.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }

        this.cts?.Dispose();
    }

    /// <summary>
    /// Fires every occurrence that is due as of <see cref="TimeProvider.GetUtcNow"/> — each occurrence strictly
    /// after the watermark and at or before now — advancing the watermark. This is the testable tick: a host
    /// drives the cadence (see <see cref="RunLoopAsync"/>) and the clock is a <see cref="TimeProvider"/>, so a
    /// test advances the clock and calls this directly. Firing is idempotent on the occurrence instant, so a
    /// re-run of a slot does not start a duplicate.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The number of occurrences fired.</returns>
    internal async ValueTask<int> FireDueAsync(CancellationToken cancellationToken)
    {
        DateTimeOffset now = this.timeProvider.GetUtcNow();
        int fired = 0;
        while (this.schedule.GetNextOccurrence(this.cursor) is { } occurrence && occurrence <= now)
        {
            await this.FireOnceAsync(occurrence, cancellationToken).ConfigureAwait(false);
            this.cursor = occurrence;
            fired++;
        }

        return fired;
    }

    /// <summary>Starts a run for a single occurrence (idempotent on the occurrence instant).</summary>
    /// <param name="occurrence">The scheduled instant this run represents.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The id of the run for this occurrence.</returns>
    internal ValueTask<WorkflowRunId> FireOnceAsync(DateTimeOffset occurrence, CancellationToken cancellationToken)
    {
        string idempotencyKey = occurrence.ToString("O", CultureInfo.InvariantCulture);
        var request = new WorkflowStartRequest(this.binding.WorkflowId, this.binding.Inputs, idempotencyKey);
        return this.start(request, cancellationToken);
    }

    private async Task RunLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (this.schedule.GetNextOccurrence(this.cursor) is not { } occurrence)
                {
                    break;
                }

                TimeSpan delay = occurrence - this.timeProvider.GetUtcNow();
                if (delay > TimeSpan.Zero)
                {
                    await Task.Delay(delay, this.timeProvider, cancellationToken).ConfigureAwait(false);
                }

                await this.FireDueAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }
}