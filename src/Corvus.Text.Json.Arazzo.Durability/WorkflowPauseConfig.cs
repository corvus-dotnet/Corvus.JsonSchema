// <copyright file="WorkflowPauseConfig.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// The ephemeral, per-advance §18 debugger pause configuration for a durable run: the stop points a single
/// re-entry of the executor honours at its step boundaries. It is never persisted — a caller sets it on the
/// run for the duration of one advance through <see cref="WorkflowRun.SetPause"/>; an advance on which no
/// pause is configured behaves exactly like an ordinary run (the default).
/// </summary>
/// <remarks>
/// The breakpoints are STATE INDICES (cursors) in the durable executor's cursor space, not step ids: a run
/// pauses when a checkpoint reaches a breakpoint cursor, having completed the previous step. The debug-run
/// layer maps step ids to cursors before setting this, mirroring the debug session's <c>afterEachStep</c> +
/// breakpoints shape.
/// </remarks>
/// <param name="AfterEachStep">Whether the run pauses at every step boundary past the cursor the advance started at (single-step).</param>
/// <param name="BreakpointCursors">The cursors (state indices) the run pauses at when a checkpoint reaches one.</param>
public readonly record struct WorkflowPauseConfig(bool AfterEachStep, IReadOnlySet<int> BreakpointCursors);