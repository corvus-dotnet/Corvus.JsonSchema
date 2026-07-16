// <copyright file="WorkflowDocumentDiagnostic.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.CodeGeneration;

/// <summary>
/// One positioned finding from <see cref="WorkflowDocumentAnalyzer"/>: where (a JSON Pointer into
/// the Arazzo document), how bad (<see cref="Severity"/>), what family of problem
/// (<see cref="Category"/>, a stable kebab-case slug the UI can group/filter by), and a
/// human-readable message.
/// </summary>
/// <param name="Severity">How bad: an <c>Error</c> blocks a correct run; a <c>Warning</c> is suspect but runnable; an <c>Info</c> is advisory.</param>
/// <param name="Category">The stable finding family: <c>duplicate-id</c>, <c>goto-target</c>, <c>depends-on</c>, <c>criterion-syntax</c>, <c>criterion-type</c>, <c>expression-syntax</c>, <c>component-reference</c>, <c>reachability</c>.</param>
/// <param name="InstancePath">The JSON Pointer to the finding's location within the document.</param>
/// <param name="Message">The human-readable description.</param>
public readonly record struct WorkflowDocumentDiagnostic(
    WorkflowDocumentDiagnosticSeverity Severity,
    string Category,
    string InstancePath,
    string Message);

/// <summary>The severity of a <see cref="WorkflowDocumentDiagnostic"/>.</summary>
public enum WorkflowDocumentDiagnosticSeverity
{
    /// <summary>The document cannot run correctly as written.</summary>
    Error,

    /// <summary>Suspect but runnable (e.g. an unreachable step, an xpath criterion this runtime does not evaluate).</summary>
    Warning,

    /// <summary>Advisory only.</summary>
    Info,
}