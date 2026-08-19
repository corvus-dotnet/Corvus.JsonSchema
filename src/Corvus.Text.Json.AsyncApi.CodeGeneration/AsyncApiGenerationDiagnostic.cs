// <copyright file="AsyncApiGenerationDiagnostic.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.AsyncApi.CodeGeneration;

/// <summary>
/// A problem the generator encountered and worked around while walking the specification.
/// </summary>
/// <remarks>
/// Generation is deliberately lenient: an entry the generator cannot use (for example a
/// <c>$ref</c> that does not resolve) is skipped rather than failing the run, and the skip
/// is recorded as a diagnostic so it is never silent. Callers decide whether diagnostics
/// are fatal; the CLI prints them as warnings and fails the run under <c>--strict</c>.
/// </remarks>
/// <param name="Severity">The severity of the diagnostic.</param>
/// <param name="Location">The specification location, as a JSON-pointer-style path (e.g. <c>#/channels/orders</c>).</param>
/// <param name="Message">The human-readable description of the problem and what the generator did about it.</param>
public readonly record struct AsyncApiGenerationDiagnostic(
    AsyncApiGenerationDiagnosticSeverity Severity,
    string Location,
    string Message);

/// <summary>
/// The severity of an <see cref="AsyncApiGenerationDiagnostic"/>.
/// </summary>
public enum AsyncApiGenerationDiagnosticSeverity
{
    /// <summary>
    /// The generator skipped or degraded something; the output omits the affected member.
    /// </summary>
    Warning,
}