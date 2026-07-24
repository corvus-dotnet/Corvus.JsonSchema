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

/// <summary>
/// Whether the emitted executor takes one <c>IMessageTransport</c> (a single channel source) or a
/// source-name → transport map with a hoisted local per channel source (ADR 0051) — the message analogue of
/// <see cref="TransportSelection"/>.
/// </summary>
/// <param name="MultiSource">Whether the workflow's channel steps span more than one AsyncAPI source.</param>
internal readonly record struct MessageTransportSelection(bool MultiSource)
{
    /// <summary>The parameter name carrying the message transport(s) on the emitted <c>ExecuteAsync</c>.</summary>
    public string ParameterName => this.MultiSource ? "messageTransports" : "messageTransport";

    /// <summary>The transport expression a channel step belonging to <paramref name="sourceName"/> uses (the hoisted local in multi-source mode, else the single parameter).</summary>
    /// <param name="sourceName">The channel source description name the step belongs to.</param>
    /// <returns>The in-scope transport expression.</returns>
    public string ForSource(string sourceName)
        => this.MultiSource ? TransportLocal(sourceName) : "messageTransport";

    /// <summary>The hoisted local name for a channel source's transport in multi-source mode.</summary>
    /// <param name="sourceName">The channel source description name.</param>
    /// <returns>A camelCase local identifier, e.g. <c>notificationsMessageTransport</c>.</returns>
    public static string TransportLocal(string sourceName)
        => EmitText.ToCamelCase(EmitText.ToPascalCase(sourceName)) + "MessageTransport";
}

/// <summary>
/// One channel (AsyncAPI) source a workflow's channel steps use: its <c>sourceDescriptions</c> name and the
/// transport protocol its source document declares (<c>servers[].protocol</c>, ADR 0051) — baked into the
/// emitted descriptor so the host binds a broker transport per channel source.
/// </summary>
/// <param name="Name">The channel source description name.</param>
/// <param name="Protocol">The declared transport protocol (e.g. <c>nats</c>).</param>
internal readonly record struct MessageSourceInfo(string Name, string Protocol);