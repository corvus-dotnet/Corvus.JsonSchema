// <copyright file="WorkflowTransportBindingException.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// Thrown when a workflow cannot be bound to its transports: a source it declares has no configured API
/// transport, it needs a message transport that is not configured, or it declares a binding shape that is
/// not yet supported. Raised at load/validation time so a misconfigured host fails fast (design §8) rather
/// than at the first step call.
/// </summary>
public sealed class WorkflowTransportBindingException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="WorkflowTransportBindingException"/> class.</summary>
    /// <param name="message">A description of the missing or unsupported binding.</param>
    public WorkflowTransportBindingException(string message)
        : base(message)
    {
    }
}