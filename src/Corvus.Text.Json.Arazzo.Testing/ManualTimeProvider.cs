// <copyright file="ManualTimeProvider.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Testing;

/// <summary>
/// A <see cref="TimeProvider"/> whose clock moves only when told to — the virtual clock behind
/// deterministic workflow simulation (workflow-designer design §8.1): timer waits advance to their
/// due moment instantly, and nothing ever waits in real time.
/// </summary>
/// <remarks>
/// Deliberately minimal rather than a package dependency: <see cref="Advance(DateTimeOffset)"/> moves
/// the clock monotonically and fires any timers that became due, which is the entirety of what the
/// simulator needs. Thread-affine by design — the simulator drives one run at a time.
/// </remarks>
public sealed class ManualTimeProvider : TimeProvider
{
    private readonly List<ManualTimer> timers = [];
    private DateTimeOffset utcNow;

    /// <summary>Initializes a new instance of the <see cref="ManualTimeProvider"/> class.</summary>
    /// <param name="startAt">The clock's initial instant (default: 2020-01-01T00:00:00Z, a fixed epoch for reproducible traces).</param>
    public ManualTimeProvider(DateTimeOffset? startAt = null)
    {
        this.utcNow = startAt ?? new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);
    }

    /// <inheritdoc/>
    public override DateTimeOffset GetUtcNow() => this.utcNow;

    /// <summary>Moves the clock forward to <paramref name="to"/> and fires timers that became due.</summary>
    /// <param name="to">The new instant; earlier-than-now values are ignored (the clock never rewinds).</param>
    public void Advance(DateTimeOffset to)
    {
        if (to <= this.utcNow)
        {
            return;
        }

        this.utcNow = to;
        ManualTimer[] due;
        lock (this.timers)
        {
            due = [.. this.timers.Where(t => t.DueAt <= to && !t.Fired)];
        }

        foreach (ManualTimer timer in due)
        {
            timer.Fire();
        }
    }

    /// <inheritdoc/>
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new ManualTimer(this, callback, state);
        timer.Change(dueTime, period);
        lock (this.timers)
        {
            this.timers.Add(timer);
        }

        return timer;
    }

    private sealed class ManualTimer(ManualTimeProvider owner, TimerCallback callback, object? state) : ITimer
    {
        public DateTimeOffset DueAt { get; private set; } = DateTimeOffset.MaxValue;

        public bool Fired { get; private set; }

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            this.DueAt = dueTime == Timeout.InfiniteTimeSpan ? DateTimeOffset.MaxValue : owner.utcNow + dueTime;
            this.Fired = false;
            if (dueTime == TimeSpan.Zero)
            {
                this.Fire();
            }

            return true;
        }

        public void Fire()
        {
            if (!this.Fired)
            {
                this.Fired = true;
                callback(state);
            }
        }

        public void Dispose() => this.Fired = true;

        public ValueTask DisposeAsync()
        {
            this.Dispose();
            return default;
        }
    }
}