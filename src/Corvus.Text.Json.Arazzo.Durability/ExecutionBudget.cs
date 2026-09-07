// <copyright file="ExecutionBudget.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// The limits every production run carries (ADR 0068): fuel, the maximum number of step executions; a wall clock, the
/// maximum age from creation; the sub-workflow depth cap; and the ceiling on a step's declared <c>retryAfter</c>.
/// </summary>
/// <remarks>
/// <para>
/// The deployment sets the ceiling, an environment may carry an <see cref="ExecutionBudgetOverride"/> that only
/// tightens it, and the effective budget is resolved when the run starts and recorded in the run's checkpoint, so a
/// later change to the environment does not move a run's bound under it. The vocabulary is the simulator's
/// (<c>maxSteps</c>, a wall clock), so a workflow bounded in the designer is bounded the same way in production.
/// </para>
/// <para>
/// Fuel is bounded by the per-step journal cap (ADR 0050): the journal is the counter the checkpoint coordinator
/// verifies against, it is exact only up to <see cref="MaxStepsCeiling"/>, and a truncated journal is over any
/// admissible budget by definition. A budget cannot be constructed above the ceiling.
/// </para>
/// </remarks>
public readonly record struct ExecutionBudget
{
    /// <summary>The largest fuel any budget may carry: the per-step journal cap, which is the counter the coordinator verifies against.</summary>
    public const int MaxStepsCeiling = 500;

    /// <summary>Gets the default wall clock, twenty-four hours from creation.</summary>
    public static readonly TimeSpan DefaultWallClock = TimeSpan.FromHours(24);

    /// <summary>Gets the default ceiling on a step's declared <c>retryAfter</c>, one hour.</summary>
    public static readonly TimeSpan DefaultRetryAfterCeiling = TimeSpan.FromHours(1);

    /// <summary>Initializes a new instance of the <see cref="ExecutionBudget"/> struct.</summary>
    /// <param name="maxSteps">Fuel: the maximum number of step executions, retries and revisits counted, from 1 to <see cref="MaxStepsCeiling"/>.</param>
    /// <param name="wallClock">The maximum age of the run from creation, positive.</param>
    /// <param name="maxSubWorkflowDepth">The sub-workflow nesting depth cap, zero or more.</param>
    /// <param name="retryAfterCeiling">The ceiling a step's declared <c>retryAfter</c> delay is clamped to, zero or more.</param>
    /// <exception cref="ArgumentOutOfRangeException">A limit is outside its admissible range.</exception>
    public ExecutionBudget(int maxSteps, TimeSpan wallClock, int maxSubWorkflowDepth, TimeSpan retryAfterCeiling)
    {
        if (maxSteps < 1 || maxSteps > MaxStepsCeiling)
        {
            throw ThrowHelper.GetExecutionBudgetOutOfRangeException(JsonPropertyNames.MaxSteps, maxSteps, 1, MaxStepsCeiling, nameof(maxSteps));
        }

        if (wallClock <= TimeSpan.Zero)
        {
            throw ThrowHelper.GetExecutionBudgetOutOfRangeException(JsonPropertyNames.WallClockSeconds, (long)wallClock.TotalSeconds, 1, long.MaxValue, nameof(wallClock));
        }

        if (maxSubWorkflowDepth < 0)
        {
            throw ThrowHelper.GetExecutionBudgetOutOfRangeException(JsonPropertyNames.MaxSubWorkflowDepth, maxSubWorkflowDepth, 0, int.MaxValue, nameof(maxSubWorkflowDepth));
        }

        if (retryAfterCeiling < TimeSpan.Zero)
        {
            throw ThrowHelper.GetExecutionBudgetOutOfRangeException(JsonPropertyNames.RetryAfterCeilingSeconds, (long)retryAfterCeiling.TotalSeconds, 0, long.MaxValue, nameof(retryAfterCeiling));
        }

        this.MaxSteps = maxSteps;
        this.WallClock = wallClock;
        this.MaxSubWorkflowDepth = maxSubWorkflowDepth;
        this.RetryAfterCeiling = retryAfterCeiling;
    }

    /// <summary>Gets the deployment default: the journal cap's worth of fuel, a day of wall clock, the depth cap every
    /// tracking run surface already honours, and an hour's retry-after ceiling.</summary>
    public static ExecutionBudget Default => new(MaxStepsCeiling, DefaultWallClock, IWorkflowRun.MaxSubWorkflowDepth, DefaultRetryAfterCeiling);

    /// <summary>Gets fuel: the maximum number of step executions.</summary>
    public int MaxSteps { get; }

    /// <summary>Gets the maximum age of the run from creation.</summary>
    public TimeSpan WallClock { get; }

    /// <summary>Gets the sub-workflow nesting depth cap.</summary>
    public int MaxSubWorkflowDepth { get; }

    /// <summary>Gets the ceiling a step's declared <c>retryAfter</c> delay is clamped to.</summary>
    public TimeSpan RetryAfterCeiling { get; }

    /// <summary>Resolves the effective budget for a run: the ceiling, tightened by an environment's override where one is present.</summary>
    /// <param name="ceiling">The deployment ceiling.</param>
    /// <param name="environmentOverride">The environment record's <c>executionBudget</c> element, or undefined.</param>
    /// <returns>The effective budget. An override that does not read (absent, or malformed on a stored record) resolves to the ceiling, never wider.</returns>
    public static ExecutionBudget Resolve(in ExecutionBudget ceiling, in JsonElement environmentOverride)
        => ExecutionBudgetOverride.TryRead(environmentOverride, out ExecutionBudgetOverride over) ? ceiling.TightenedBy(over) : ceiling;

    /// <summary>Tightens this budget by an override: each limit the override names becomes the smaller of the two.</summary>
    /// <param name="over">The override.</param>
    /// <returns>The tightened budget.</returns>
    public ExecutionBudget TightenedBy(in ExecutionBudgetOverride over)
        => new(
            over.MaxSteps is { } steps ? Math.Min(steps, this.MaxSteps) : this.MaxSteps,
            over.WallClock is { } clock && clock < this.WallClock ? clock : this.WallClock,
            over.MaxSubWorkflowDepth is { } depth ? Math.Min(depth, this.MaxSubWorkflowDepth) : this.MaxSubWorkflowDepth,
            over.RetryAfterCeiling is { } retry && retry < this.RetryAfterCeiling ? retry : this.RetryAfterCeiling);

    /// <summary>Writes the budget as the checkpoint's <c>budget</c> object.</summary>
    /// <param name="writer">The writer, positioned where a property may be written.</param>
    public void WriteTo(Utf8JsonWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteStartObject(JsonPropertyNames.BudgetUtf8);
        writer.WriteNumber(JsonPropertyNames.MaxStepsUtf8, this.MaxSteps);
        writer.WriteNumber(JsonPropertyNames.WallClockMsUtf8, (long)this.WallClock.TotalMilliseconds);
        writer.WriteNumber(JsonPropertyNames.MaxSubWorkflowDepthUtf8, this.MaxSubWorkflowDepth);
        writer.WriteNumber(JsonPropertyNames.RetryAfterCeilingMsUtf8, (long)this.RetryAfterCeiling.TotalMilliseconds);
        writer.WriteEndObject();
    }

    /// <summary>Reads a checkpoint's <c>budget</c> object.</summary>
    /// <param name="element">The checkpoint's <c>budget</c> element, or undefined.</param>
    /// <param name="budget">The budget, when the element carries one.</param>
    /// <returns><see langword="true"/> if a well-formed budget was read.</returns>
    public static bool TryRead(in JsonElement element, out ExecutionBudget budget)
    {
        if (element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(JsonPropertyNames.MaxStepsUtf8, out JsonElement steps) && TryReadInteger(steps, out long maxSteps)
            && element.TryGetProperty(JsonPropertyNames.WallClockMsUtf8, out JsonElement clock) && TryReadInteger(clock, out long wallClockMs)
            && element.TryGetProperty(JsonPropertyNames.MaxSubWorkflowDepthUtf8, out JsonElement depth) && TryReadInteger(depth, out long maxDepth)
            && element.TryGetProperty(JsonPropertyNames.RetryAfterCeilingMsUtf8, out JsonElement retry) && TryReadInteger(retry, out long retryMs)
            && maxSteps >= 1 && maxSteps <= MaxStepsCeiling && wallClockMs > 0 && maxDepth >= 0 && maxDepth <= int.MaxValue && retryMs >= 0)
        {
            budget = new ExecutionBudget((int)maxSteps, TimeSpan.FromMilliseconds(wallClockMs), (int)maxDepth, TimeSpan.FromMilliseconds(retryMs));
            return true;
        }

        budget = default;
        return false;
    }

    /// <summary>Reads an integral JSON number, answering <see langword="false"/> for any other value kind or a non-integral
    /// number rather than throwing, since a stored record may carry anything and an authored one is refused with a message.</summary>
    /// <param name="value">The element.</param>
    /// <param name="integer">The integer read.</param>
    /// <returns><see langword="true"/> if the element is an integral number.</returns>
    internal static bool TryReadInteger(in JsonElement value, out long integer)
    {
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out integer))
        {
            return true;
        }

        integer = 0;
        return false;
    }

    /// <summary>The property names of the checkpoint's <c>budget</c> object and of an environment's <c>executionBudget</c> override.</summary>
    public static class JsonPropertyNames
    {
        /// <summary>The checkpoint property carrying the resolved budget.</summary>
        public const string Budget = "budget";

        /// <summary>Fuel, on both the checkpoint and the override.</summary>
        public const string MaxSteps = "maxSteps";

        /// <summary>The wall clock on the checkpoint, in milliseconds.</summary>
        public const string WallClockMs = "wallClockMs";

        /// <summary>The wall clock on the override, in seconds.</summary>
        public const string WallClockSeconds = "wallClockSeconds";

        /// <summary>The depth cap, on both the checkpoint and the override.</summary>
        public const string MaxSubWorkflowDepth = "maxSubWorkflowDepth";

        /// <summary>The retry-after ceiling on the checkpoint, in milliseconds.</summary>
        public const string RetryAfterCeilingMs = "retryAfterCeilingMs";

        /// <summary>The retry-after ceiling on the override, in seconds.</summary>
        public const string RetryAfterCeilingSeconds = "retryAfterCeilingSeconds";

        /// <summary>Gets <see cref="Budget"/> as UTF-8.</summary>
        public static ReadOnlySpan<byte> BudgetUtf8 => "budget"u8;

        /// <summary>Gets <see cref="MaxSteps"/> as UTF-8.</summary>
        public static ReadOnlySpan<byte> MaxStepsUtf8 => "maxSteps"u8;

        /// <summary>Gets <see cref="WallClockMs"/> as UTF-8.</summary>
        public static ReadOnlySpan<byte> WallClockMsUtf8 => "wallClockMs"u8;

        /// <summary>Gets <see cref="WallClockSeconds"/> as UTF-8.</summary>
        public static ReadOnlySpan<byte> WallClockSecondsUtf8 => "wallClockSeconds"u8;

        /// <summary>Gets <see cref="MaxSubWorkflowDepth"/> as UTF-8.</summary>
        public static ReadOnlySpan<byte> MaxSubWorkflowDepthUtf8 => "maxSubWorkflowDepth"u8;

        /// <summary>Gets <see cref="RetryAfterCeilingMs"/> as UTF-8.</summary>
        public static ReadOnlySpan<byte> RetryAfterCeilingMsUtf8 => "retryAfterCeilingMs"u8;

        /// <summary>Gets <see cref="RetryAfterCeilingSeconds"/> as UTF-8.</summary>
        public static ReadOnlySpan<byte> RetryAfterCeilingSecondsUtf8 => "retryAfterCeilingSeconds"u8;
    }
}

/// <summary>
/// An environment's execution-budget override (ADR 0068): each limit it names tightens the deployment ceiling, and a
/// limit it omits is the ceiling's. Read from the environment record's <c>executionBudget</c> object.
/// </summary>
/// <param name="MaxSteps">Fuel, or <see langword="null"/> to keep the ceiling's.</param>
/// <param name="WallClock">The wall clock, or <see langword="null"/> to keep the ceiling's.</param>
/// <param name="MaxSubWorkflowDepth">The depth cap, or <see langword="null"/> to keep the ceiling's.</param>
/// <param name="RetryAfterCeiling">The retry-after ceiling, or <see langword="null"/> to keep the ceiling's.</param>
public readonly record struct ExecutionBudgetOverride(int? MaxSteps, TimeSpan? WallClock, int? MaxSubWorkflowDepth, TimeSpan? RetryAfterCeiling)
{
    /// <summary>Reads an override from an environment record's <c>executionBudget</c> element, refusing nothing: a
    /// malformed or out-of-range limit reads as no override at all, so a stored record can only tighten.</summary>
    /// <param name="element">The <c>executionBudget</c> element, or undefined.</param>
    /// <param name="over">The override read.</param>
    /// <returns><see langword="true"/> if the element is an object; the limits it names are those that read well-formed.</returns>
    public static bool TryRead(in JsonElement element, out ExecutionBudgetOverride over)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            over = default;
            return false;
        }

        int? maxSteps = element.TryGetProperty(ExecutionBudget.JsonPropertyNames.MaxStepsUtf8, out JsonElement steps) && ExecutionBudget.TryReadInteger(steps, out long s) && s >= 1 && s <= int.MaxValue ? (int)s : null;
        TimeSpan? wallClock = element.TryGetProperty(ExecutionBudget.JsonPropertyNames.WallClockSecondsUtf8, out JsonElement clock) && ExecutionBudget.TryReadInteger(clock, out long c) && c >= 1 ? TimeSpan.FromSeconds(c) : null;
        int? depth = element.TryGetProperty(ExecutionBudget.JsonPropertyNames.MaxSubWorkflowDepthUtf8, out JsonElement d) && ExecutionBudget.TryReadInteger(d, out long dv) && dv >= 0 && dv <= int.MaxValue ? (int)dv : null;
        TimeSpan? retry = element.TryGetProperty(ExecutionBudget.JsonPropertyNames.RetryAfterCeilingSecondsUtf8, out JsonElement r) && ExecutionBudget.TryReadInteger(r, out long rv) && rv >= 0 ? TimeSpan.FromSeconds(rv) : null;
        over = new ExecutionBudgetOverride(maxSteps, wallClock, depth, retry);
        return true;
    }

    /// <summary>Validates an override authored through the API against the deployment ceiling: every limit must be
    /// well-formed and no limit may exceed the ceiling's, since an override only tightens.</summary>
    /// <param name="element">The request body's <c>executionBudget</c> element.</param>
    /// <param name="ceiling">The deployment ceiling.</param>
    /// <exception cref="ArgumentException">A limit is malformed, out of range, or wider than the ceiling.</exception>
    public static void ValidateAuthored(in JsonElement element, in ExecutionBudget ceiling)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw ThrowHelper.GetExecutionBudgetNotAnObjectException(ExecutionBudget.JsonPropertyNames.Budget);
        }

        RequireWithin(element, ExecutionBudget.JsonPropertyNames.MaxStepsUtf8, ExecutionBudget.JsonPropertyNames.MaxSteps, 1, ceiling.MaxSteps);
        RequireWithin(element, ExecutionBudget.JsonPropertyNames.WallClockSecondsUtf8, ExecutionBudget.JsonPropertyNames.WallClockSeconds, 1, (long)ceiling.WallClock.TotalSeconds);
        RequireWithin(element, ExecutionBudget.JsonPropertyNames.MaxSubWorkflowDepthUtf8, ExecutionBudget.JsonPropertyNames.MaxSubWorkflowDepth, 0, ceiling.MaxSubWorkflowDepth);
        RequireWithin(element, ExecutionBudget.JsonPropertyNames.RetryAfterCeilingSecondsUtf8, ExecutionBudget.JsonPropertyNames.RetryAfterCeilingSeconds, 0, (long)ceiling.RetryAfterCeiling.TotalSeconds);
    }

    private static void RequireWithin(in JsonElement element, ReadOnlySpan<byte> nameUtf8, string name, long minimum, long maximum)
    {
        if (!element.TryGetProperty(nameUtf8, out JsonElement value))
        {
            return;
        }

        if (!ExecutionBudget.TryReadInteger(value, out long limit))
        {
            throw ThrowHelper.GetExecutionBudgetNotAnIntegerException(name);
        }

        if (limit < minimum || limit > maximum)
        {
            throw ThrowHelper.GetExecutionBudgetOutOfRangeException(name, limit, minimum, maximum, name);
        }
    }
}