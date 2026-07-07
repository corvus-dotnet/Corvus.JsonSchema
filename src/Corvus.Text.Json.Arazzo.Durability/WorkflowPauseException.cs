// <copyright file="WorkflowPauseException.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// The control-flow signal a durable run throws from <see cref="WorkflowRun.CheckpointAsync"/> to unwind an
/// advance that reached a §18 debugger pause point (an after-each-step stop or a breakpoint). The run is
/// already persisted <see cref="WorkflowRunStatus.Suspended"/> with a <see cref="WorkflowWaitKind.Pause"/>
/// wait before the throw, so the resumer catches this to stop driving the executor without treating the
/// pause as a fault — the durable analogue of the simulator's pause throw. Never escapes the resumer.
/// </summary>
internal sealed class WorkflowPauseException(int cursor) : Exception
{
    /// <summary>Gets the cursor (state index) the run paused at.</summary>
    public int Cursor { get; } = cursor;
}