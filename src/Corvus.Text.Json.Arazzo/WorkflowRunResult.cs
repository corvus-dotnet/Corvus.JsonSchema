// <copyright file="WorkflowRunResult.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo;

/// <summary>The outcome kind of a durable workflow run (plan §9.4).</summary>
public enum WorkflowRunResultKind
{
    /// <summary>The run finished successfully; <see cref="WorkflowRunResult{T}.Outputs"/> holds the workflow outputs.</summary>
    Completed,

    /// <summary>The run hit a terminal-but-recoverable failure; <see cref="WorkflowRunResult{T}.Fault"/> describes it.</summary>
    Faulted,

    /// <summary>The run is waiting for an external event; <see cref="WorkflowRunResult{T}.Wait"/> describes why.</summary>
    Suspended,
}

/// <summary>
/// The tri-state result a durable executor returns (plan §9.4): the run either <em>completed</em> with its
/// outputs, <em>faulted</em> with a fault record, or <em>suspended</em> pending an external event (a due
/// timer or a correlated message). The non-durable executor still returns <c>ValueTask&lt;TOutputs&gt;</c>
/// unchanged; this type is only produced when an executor runs in durable mode.
/// </summary>
/// <typeparam name="T">The workflow outputs type.</typeparam>
public readonly struct WorkflowRunResult<T>
{
    private WorkflowRunResult(WorkflowRunResultKind kind, T outputs, WorkflowFault fault, WorkflowWait wait)
    {
        this.Kind = kind;
        this.Outputs = outputs;
        this.Fault = fault;
        this.Wait = wait;
    }

    /// <summary>Gets the outcome kind.</summary>
    public WorkflowRunResultKind Kind { get; }

    /// <summary>Gets the workflow outputs (valid only when <see cref="IsCompleted"/>).</summary>
    public T Outputs { get; }

    /// <summary>Gets the fault record (valid only when <see cref="IsFaulted"/>).</summary>
    public WorkflowFault Fault { get; }

    /// <summary>Gets the wait describing why the run is suspended (valid only when <see cref="IsSuspended"/>).</summary>
    public WorkflowWait Wait { get; }

    /// <summary>Gets a value indicating whether the run completed successfully.</summary>
    public bool IsCompleted => this.Kind == WorkflowRunResultKind.Completed;

    /// <summary>Gets a value indicating whether the run faulted.</summary>
    public bool IsFaulted => this.Kind == WorkflowRunResultKind.Faulted;

    /// <summary>Gets a value indicating whether the run suspended.</summary>
    public bool IsSuspended => this.Kind == WorkflowRunResultKind.Suspended;

    /// <summary>Creates a completed result.</summary>
    /// <param name="outputs">The workflow outputs.</param>
    /// <returns>The result.</returns>
    public static WorkflowRunResult<T> Completed(T outputs) => new(WorkflowRunResultKind.Completed, outputs, default, default);

    /// <summary>Creates a faulted result.</summary>
    /// <param name="fault">The fault record.</param>
    /// <returns>The result.</returns>
    public static WorkflowRunResult<T> Faulted(WorkflowFault fault) => new(WorkflowRunResultKind.Faulted, default!, fault, default);

    /// <summary>Creates a suspended result.</summary>
    /// <param name="wait">The wait describing why the run suspended.</param>
    /// <returns>The result.</returns>
    public static WorkflowRunResult<T> Suspended(WorkflowWait wait) => new(WorkflowRunResultKind.Suspended, default!, default, wait);
}