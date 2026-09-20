// <copyright file="AuditAppendException.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// An audit record could not be appended to the sink (ADR 0069). The action the record describes has already happened
/// and stands; what failed is the evidence of it, which a secured control plane surfaces as the request's failure.
/// </summary>
public sealed class AuditAppendException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="AuditAppendException"/> class.</summary>
    /// <param name="message">The message.</param>
    /// <param name="innerException">The sink's failure.</param>
    public AuditAppendException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}