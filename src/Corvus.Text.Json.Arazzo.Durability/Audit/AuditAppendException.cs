// <copyright file="AuditAppendException.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// An audit record could not be appended to the sink (ADR 0069). For a mutation, the action the record describes has
/// already happened and stands; what failed is the evidence of it, which a secured control plane surfaces as the request's
/// failure. For a read that discloses a payload (ADR 0070) the record comes first, so the read is refused undisclosed.
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

    /// <summary>
    /// Gets what the record that could not be appended was evidence of. A mutation has already happened and stands; a read
    /// has not been answered, since a disclosure is recorded before it is made, so nothing was disclosed.
    /// </summary>
    public AuditEntryKind Kind { get; init; }
}