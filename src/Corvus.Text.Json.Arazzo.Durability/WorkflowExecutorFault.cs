// <copyright file="WorkflowExecutorFault.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Execution;

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// The error types recorded on a run that the execution seam ended, because its executor could not be obtained, its
/// transports could not be bound, or the executor failed in a way nothing in the workflow handled.
/// </summary>
public static class WorkflowExecutorFault
{
    /// <summary>
    /// The executor threw something that was not a step failure, a pause, a budget fault or a credential fault.
    /// </summary>
    /// <remarks>
    /// The fault carries this fixed value and not the exception's message. A run record is read by whoever may see the
    /// run, and an exception message can name hosts, paths and internals that reader has no business with. The
    /// exception itself goes to the executor's activity, where the operator's telemetry has it.
    /// </remarks>
    public const string Unhandled = "executor-unhandled";

    /// <summary>
    /// The run's executor could not be resolved, and would not be on any later attempt: the version is not in the
    /// catalog or is not runnable, its content hash diverges, or its assembly failed the loader's verification.
    /// </summary>
    /// <remarks>
    /// Recorded for a refusal only (<see cref="IsRefusal"/>). A failure to reach the artifact source is not one, and
    /// leaves the run live to be retried.
    /// </remarks>
    public const string Unresolvable = "executor-unresolvable";

    /// <summary>
    /// The run's transports could not be bound: a source the workflow calls has no binding in the run's environment,
    /// or a channel transport could not be built. An operator supplies the binding and resumes the run.
    /// </summary>
    public const string TransportUnbound = "transport-unbound";

    /// <summary>
    /// Whether a failure to resolve an executor is a refusal, the same answer on every attempt, and so ends the run.
    /// </summary>
    /// <param name="exception">The failure resolution threw.</param>
    /// <returns><see langword="true"/> for a resolver's refusal or the loader's; <see langword="false"/> for anything
    /// else, which is taken to be the artifact source failing and is retried.</returns>
    public static bool IsRefusal(Exception exception)
        => exception is WorkflowExecutorUnresolvableException or WorkflowExecutorLoadException;
}