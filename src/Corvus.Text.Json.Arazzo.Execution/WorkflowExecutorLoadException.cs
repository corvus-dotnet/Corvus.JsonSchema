// <copyright file="WorkflowExecutorLoadException.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Execution;

/// <summary>
/// Thrown when a workflow executor assembly cannot be loaded: an integrity mismatch (assembly digest or
/// package hash), an unsupported target framework, a malformed manifest, or an entry type that is missing or
/// does not implement <see cref="IHostedWorkflow"/>.
/// </summary>
public sealed class WorkflowExecutorLoadException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="WorkflowExecutorLoadException"/> class.</summary>
    /// <param name="message">The error message.</param>
    public WorkflowExecutorLoadException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="WorkflowExecutorLoadException"/> class.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The underlying cause.</param>
    public WorkflowExecutorLoadException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}