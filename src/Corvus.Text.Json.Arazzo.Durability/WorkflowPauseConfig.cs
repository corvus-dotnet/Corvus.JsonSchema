// <copyright file="WorkflowPauseConfig.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// The §18 debugger pause configuration for a durable run: the stop points the executor honours at its step
/// boundaries. A caller sets it on the run through <see cref="WorkflowRun.SetPause"/> for the current advance,
/// or when marking a paused run resume-claimable through <see cref="WorkflowRun.RequestResumeAsync"/>. It is
/// persisted into the checkpoint so a runner that later claims the run — a <em>different</em> process — applies
/// the same stops without re-supplying them, and is re-persisted across each pause so a single-step /
/// next-breakpoint carries; a later resume overwrites it. An advance on which no pause is configured persists
/// none and behaves exactly like an ordinary run (the default).
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