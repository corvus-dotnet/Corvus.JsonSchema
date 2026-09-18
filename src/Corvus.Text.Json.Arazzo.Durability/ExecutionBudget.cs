// <copyright file="ExecutionBudget.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
using Corvus.Text.Json.Internal;

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// The limits every production run carries (ADR 0068): fuel, the maximum number of step executions; a wall clock, the
/// maximum age from creation; the sub-workflow depth cap; the ceiling on a step's declared <c>retryAfter</c>; and the
/// transport bounds on a single step, the longest its request may take and the largest response it may read.
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
/// <para>
/// The transport bounds limit one step where fuel limits how many. The runner enforces them on the request it sends,
/// and nothing in a checkpoint lets the control plane verify them, so unlike fuel and the wall clock they are not
/// budget faults: a step that breaches one has failed, and its <c>onFailure</c> actions decide what happens next. The
/// step timeout is bounded by <see cref="StepTimeoutCeiling"/> so that the run-path clients can carry a fixed backstop
/// timeout above it.
/// </para>
/// </remarks>
public readonly record struct ExecutionBudget
{
    /// <summary>The largest fuel any budget may carry: the per-step journal cap, which is the counter the coordinator verifies against.</summary>
    public const int MaxStepsCeiling = 500;

    /// <summary>Gets the longest step timeout any budget may carry, ten minutes. The run-path HTTP clients set their own
    /// timeout above this, so the budget's bound is always the one that fires.</summary>
    public static readonly TimeSpan StepTimeoutCeiling = TimeSpan.FromMinutes(10);

    /// <summary>Gets the timeout a run-path HTTP client carries, fifteen minutes: a finite backstop set above
    /// <see cref="StepTimeoutCeiling"/>, so that the budget's step timeout is always the bound that fires and a client is
    /// still never left to wait without limit.</summary>
    public static readonly TimeSpan TransportClientTimeout = TimeSpan.FromMinutes(15);

    /// <summary>Gets the default step timeout, one hundred seconds.</summary>
    public static readonly TimeSpan DefaultStepTimeout = TimeSpan.FromSeconds(100);

    /// <summary>The default largest response a step may read, sixteen mebibytes.</summary>
    public const long DefaultMaxResponseBytes = 16L * 1024 * 1024;

    /// <summary>Gets the default wall clock, twenty-four hours from creation.</summary>
    public static readonly TimeSpan DefaultWallClock = TimeSpan.FromHours(24);

    /// <summary>Gets the default ceiling on a step's declared <c>retryAfter</c>, one hour.</summary>
    public static readonly TimeSpan DefaultRetryAfterCeiling = TimeSpan.FromHours(1);

    /// <summary>Initializes a new instance of the <see cref="ExecutionBudget"/> struct.</summary>
    /// <param name="maxSteps">Fuel: the maximum number of step executions, retries and revisits counted, from 1 to <see cref="MaxStepsCeiling"/>.</param>
    /// <param name="wallClock">The maximum age of the run from creation, positive.</param>
    /// <param name="maxSubWorkflowDepth">The sub-workflow nesting depth cap, zero or more.</param>
    /// <param name="retryAfterCeiling">The ceiling a step's declared <c>retryAfter</c> delay is clamped to, zero or more.</param>
    /// <param name="stepTimeout">The longest a single step's request may take, positive and no more than <see cref="StepTimeoutCeiling"/>.</param>
    /// <param name="maxResponseBytes">The largest response body a single step may read, in bytes, positive.</param>
    /// <exception cref="ArgumentOutOfRangeException">A limit is outside its admissible range.</exception>
    public ExecutionBudget(int maxSteps, TimeSpan wallClock, int maxSubWorkflowDepth, TimeSpan retryAfterCeiling, TimeSpan stepTimeout, long maxResponseBytes)
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

        if (stepTimeout <= TimeSpan.Zero || stepTimeout > StepTimeoutCeiling)
        {
            throw ThrowHelper.GetExecutionBudgetOutOfRangeException(JsonPropertyNames.StepTimeoutSeconds, (long)stepTimeout.TotalSeconds, 1, (long)StepTimeoutCeiling.TotalSeconds, nameof(stepTimeout));
        }

        if (maxResponseBytes < 1)
        {
            throw ThrowHelper.GetExecutionBudgetOutOfRangeException(JsonPropertyNames.MaxResponseBytes, maxResponseBytes, 1, long.MaxValue, nameof(maxResponseBytes));
        }

        this.MaxSteps = maxSteps;
        this.WallClock = wallClock;
        this.MaxSubWorkflowDepth = maxSubWorkflowDepth;
        this.RetryAfterCeiling = retryAfterCeiling;
        this.StepTimeout = stepTimeout;
        this.MaxResponseBytes = maxResponseBytes;
    }

    /// <summary>Gets the deployment default: the journal cap's worth of fuel, a day of wall clock, the depth cap every
    /// tracking run surface already honours, an hour's retry-after ceiling, a hundred-second step timeout and a
    /// sixteen-mebibyte response.</summary>
    public static ExecutionBudget Default => new(MaxStepsCeiling, DefaultWallClock, IWorkflowRun.MaxSubWorkflowDepth, DefaultRetryAfterCeiling, DefaultStepTimeout, DefaultMaxResponseBytes);

    /// <summary>Gets fuel: the maximum number of step executions.</summary>
    public int MaxSteps { get; }

    /// <summary>Gets the maximum age of the run from creation.</summary>
    public TimeSpan WallClock { get; }

    /// <summary>Gets the sub-workflow nesting depth cap.</summary>
    public int MaxSubWorkflowDepth { get; }

    /// <summary>Gets the ceiling a step's declared <c>retryAfter</c> delay is clamped to.</summary>
    public TimeSpan RetryAfterCeiling { get; }

    /// <summary>Gets the longest a single step's request may take, from the send to the response having been read.</summary>
    public TimeSpan StepTimeout { get; }

    /// <summary>Gets the largest response body a single step may read, in bytes.</summary>
    public long MaxResponseBytes { get; }

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
            over.RetryAfterCeiling is { } retry && retry < this.RetryAfterCeiling ? retry : this.RetryAfterCeiling,
            over.StepTimeout is { } timeout && timeout < this.StepTimeout ? timeout : this.StepTimeout,
            over.MaxResponseBytes is { } bytes ? Math.Min(bytes, this.MaxResponseBytes) : this.MaxResponseBytes);

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
        writer.WriteNumber(JsonPropertyNames.StepTimeoutMsUtf8, (long)this.StepTimeout.TotalMilliseconds);
        writer.WriteNumber(JsonPropertyNames.MaxResponseBytesUtf8, this.MaxResponseBytes);
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
            && element.TryGetProperty(JsonPropertyNames.StepTimeoutMsUtf8, out JsonElement timeout) && TryReadInteger(timeout, out long timeoutMs)
            && element.TryGetProperty(JsonPropertyNames.MaxResponseBytesUtf8, out JsonElement bytes) && TryReadInteger(bytes, out long maxBytes)
            && IsAdmissible(maxSteps, wallClockMs, maxDepth, retryMs, timeoutMs, maxBytes))
        {
            budget = new ExecutionBudget((int)maxSteps, TimeSpan.FromMilliseconds(wallClockMs), (int)maxDepth, TimeSpan.FromMilliseconds(retryMs), TimeSpan.FromMilliseconds(timeoutMs), maxBytes);
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
    /// <summary>
    /// Reads a budget from a forward-only reader positioned on the budget object's start, for the checkpoint
    /// serializer's facts scan (which never parses the document). Applies the same admissibility rules as
    /// <see cref="TryRead(in JsonElement, out ExecutionBudget)"/>; an inadmissible or incomplete object reads as no
    /// budget. The reader is left on the object's end token either way.
    /// </summary>
    /// <param name="reader">The reader, positioned on <see cref="JsonTokenType.StartObject"/>.</param>
    /// <param name="budget">The budget read.</param>
    /// <returns><see langword="true"/> when the object is an admissible budget.</returns>
    internal static bool TryRead(ref Utf8JsonReader reader, out ExecutionBudget budget)
    {
        long maxSteps = -1;
        long wallClockMs = -1;
        long maxDepth = -1;
        long retryMs = -1;
        long timeoutMs = -1;
        long maxBytes = -1;
        bool wellFormed = reader.TokenType == JsonTokenType.StartObject;
        while (wellFormed && reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            int which = reader.ValueTextEquals(JsonPropertyNames.MaxStepsUtf8) ? 0
                : reader.ValueTextEquals(JsonPropertyNames.WallClockMsUtf8) ? 1
                : reader.ValueTextEquals(JsonPropertyNames.MaxSubWorkflowDepthUtf8) ? 2
                : reader.ValueTextEquals(JsonPropertyNames.RetryAfterCeilingMsUtf8) ? 3
                : reader.ValueTextEquals(JsonPropertyNames.StepTimeoutMsUtf8) ? 4
                : reader.ValueTextEquals(JsonPropertyNames.MaxResponseBytesUtf8) ? 5
                : -1;
            if (!reader.Read())
            {
                break;
            }

            if (which < 0)
            {
                reader.Skip();
                continue;
            }

            if (reader.TokenType != JsonTokenType.Number || !reader.TryGetInt64(out long value))
            {
                wellFormed = false;
                reader.Skip();
                continue;
            }

            switch (which)
            {
                case 0: maxSteps = value; break;
                case 1: wallClockMs = value; break;
                case 2: maxDepth = value; break;
                case 3: retryMs = value; break;
                case 4: timeoutMs = value; break;
                default: maxBytes = value; break;
            }
        }

        if (wellFormed && IsAdmissible(maxSteps, wallClockMs, maxDepth, retryMs, timeoutMs, maxBytes))
        {
            budget = new ExecutionBudget((int)maxSteps, TimeSpan.FromMilliseconds(wallClockMs), (int)maxDepth, TimeSpan.FromMilliseconds(retryMs), TimeSpan.FromMilliseconds(timeoutMs), maxBytes);
            return true;
        }

        budget = default;
        return false;
    }

    private static bool IsAdmissible(long maxSteps, long wallClockMs, long maxDepth, long retryMs, long timeoutMs, long maxBytes)
        => maxSteps >= 1 && maxSteps <= MaxStepsCeiling
            && wallClockMs > 0
            && maxDepth >= 0 && maxDepth <= int.MaxValue
            && retryMs >= 0
            && timeoutMs > 0 && timeoutMs <= (long)StepTimeoutCeiling.TotalMilliseconds
            && maxBytes >= 1;

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

        /// <summary>The step timeout on the checkpoint, in milliseconds.</summary>
        public const string StepTimeoutMs = "stepTimeoutMs";

        /// <summary>The step timeout on the override, in seconds.</summary>
        public const string StepTimeoutSeconds = "stepTimeoutSeconds";

        /// <summary>The largest response a step may read, in bytes, on both the checkpoint and the override.</summary>
        public const string MaxResponseBytes = "maxResponseBytes";

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

        /// <summary>Gets <see cref="StepTimeoutMs"/> as UTF-8.</summary>
        public static ReadOnlySpan<byte> StepTimeoutMsUtf8 => "stepTimeoutMs"u8;

        /// <summary>Gets <see cref="StepTimeoutSeconds"/> as UTF-8.</summary>
        public static ReadOnlySpan<byte> StepTimeoutSecondsUtf8 => "stepTimeoutSeconds"u8;

        /// <summary>Gets <see cref="MaxResponseBytes"/> as UTF-8.</summary>
        public static ReadOnlySpan<byte> MaxResponseBytesUtf8 => "maxResponseBytes"u8;
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
/// <param name="StepTimeout">The step timeout, or <see langword="null"/> to keep the ceiling's.</param>
/// <param name="MaxResponseBytes">The largest response a step may read, or <see langword="null"/> to keep the ceiling's.</param>
public readonly record struct ExecutionBudgetOverride(int? MaxSteps, TimeSpan? WallClock, int? MaxSubWorkflowDepth, TimeSpan? RetryAfterCeiling, TimeSpan? StepTimeout, long? MaxResponseBytes)
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
        TimeSpan? timeout = element.TryGetProperty(ExecutionBudget.JsonPropertyNames.StepTimeoutSecondsUtf8, out JsonElement t) && ExecutionBudget.TryReadInteger(t, out long tv) && tv >= 1 && tv <= (long)ExecutionBudget.StepTimeoutCeiling.TotalSeconds ? TimeSpan.FromSeconds(tv) : null;
        long? bytes = element.TryGetProperty(ExecutionBudget.JsonPropertyNames.MaxResponseBytesUtf8, out JsonElement b) && ExecutionBudget.TryReadInteger(b, out long bv) && bv >= 1 ? bv : null;
        over = new ExecutionBudgetOverride(maxSteps, wallClock, depth, retry, timeout, bytes);
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
        RequireWithin(element, ExecutionBudget.JsonPropertyNames.StepTimeoutSecondsUtf8, ExecutionBudget.JsonPropertyNames.StepTimeoutSeconds, 1, (long)ceiling.StepTimeout.TotalSeconds);
        RequireWithin(element, ExecutionBudget.JsonPropertyNames.MaxResponseBytesUtf8, ExecutionBudget.JsonPropertyNames.MaxResponseBytes, 1, ceiling.MaxResponseBytes);
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