// <copyright file="WorkflowStepFailedException.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo;

/// <summary>
/// Thrown when a workflow step fails — for example, when its <c>successCriteria</c> are not satisfied
/// and the step's failure behaviour is to end the workflow.
/// </summary>
public class WorkflowStepFailedException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WorkflowStepFailedException"/> class.
    /// </summary>
    public WorkflowStepFailedException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkflowStepFailedException"/> class.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public WorkflowStepFailedException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkflowStepFailedException"/> class.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of this exception.</param>
    public WorkflowStepFailedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkflowStepFailedException"/> class for a step.
    /// </summary>
    /// <param name="stepId">The id of the step that failed.</param>
    /// <param name="message">The message that describes the failure.</param>
    public WorkflowStepFailedException(string stepId, string message)
        : base(message)
    {
        this.StepId = stepId;
    }

    /// <summary>Gets the id of the step that failed, if known.</summary>
    public string? StepId { get; }
}