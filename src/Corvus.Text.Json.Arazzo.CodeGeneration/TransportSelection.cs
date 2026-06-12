// <copyright file="TransportSelection.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.CodeGeneration;

/// <summary>
/// How an emitted executor obtains the <c>IApiTransport</c> for an operation step. A single-API-source
/// document keeps the original single <c>transport</c> parameter (zero overhead); a multi-source document
/// passes a source-name → transport map and selects the transport per source. The choice is document-level,
/// not per-workflow, so a workflow can hand the whole map to a sub-workflow that uses a different source.
/// </summary>
/// <param name="MultiSource"><see langword="true"/> when the document declares more than one API source.</param>
internal readonly record struct TransportSelection(bool MultiSource)
{
    /// <summary>The parameter name carrying the transport(s) on the emitted <c>ExecuteAsync</c>.</summary>
    public string ParameterName => this.MultiSource ? "transports" : "transport";

    /// <summary>The argument an executor passes to a sub-workflow's <c>ExecuteAsync</c> (the same shape it received).</summary>
    public string SubWorkflowArgument => this.ParameterName;

    /// <summary>The transport expression an operation belonging to <paramref name="sourceName"/> uses (the hoisted local in multi-source mode, else the single parameter).</summary>
    /// <param name="sourceName">The source description name the operation belongs to.</param>
    /// <returns>The in-scope transport expression.</returns>
    public string ForSource(string sourceName)
        => this.MultiSource ? TransportLocal(sourceName) : "transport";

    /// <summary>The hoisted local name for a source's transport in multi-source mode.</summary>
    /// <param name="sourceName">The source description name.</param>
    /// <returns>A camelCase local identifier, e.g. <c>petstoreTransport</c>.</returns>
    public static string TransportLocal(string sourceName)
        => EmitText.ToCamelCase(EmitText.ToPascalCase(sourceName)) + "Transport";
}