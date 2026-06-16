// <copyright file="SourceCredentialExpiredException.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo;

/// <summary>
/// Thrown at credential-bind time when the source credential a run is entitled to use has expired (design §13.2/§13.3).
/// A durable executor catches this and records a typed <see cref="ErrorType"/> fault rather than failing opaquely, so
/// the run is <strong>Faulted</strong> and resumable: an operator rotates the secret in the store and resumes, and the
/// re-bound run picks up the fresh credential.
/// </summary>
/// <remarks>
/// The runner's credential cache raises this from the bind path; it carries the offending <see cref="SourceName"/> but
/// never any secret material. It is distinct from an ordinary <see cref="WorkflowStepFailedException"/> so the control
/// plane can filter the credential-expired faults that an operator can clear by rotation.
/// </remarks>
public sealed class SourceCredentialExpiredException : Exception
{
    /// <summary>The typed run-fault error this maps to — distinguishable and filterable in the control plane.</summary>
    public const string ErrorType = "credentials-expired";

    /// <summary>Initializes a new instance of the <see cref="SourceCredentialExpiredException"/> class.</summary>
    /// <param name="sourceName">The Arazzo source description name whose credential has expired.</param>
    public SourceCredentialExpiredException(string sourceName)
        : base($"The source credential for '{sourceName}' has expired; rotate it in the secret store and resume the run.")
        => this.SourceName = sourceName;

    /// <summary>Initializes a new instance of the <see cref="SourceCredentialExpiredException"/> class.</summary>
    /// <param name="sourceName">The Arazzo source description name whose credential has expired.</param>
    /// <param name="message">The message that describes the error.</param>
    public SourceCredentialExpiredException(string sourceName, string message)
        : base(message)
        => this.SourceName = sourceName;

    /// <summary>Gets the Arazzo source description name whose credential has expired.</summary>
    public string SourceName { get; }
}