// <copyright file="WorkflowDescriptor.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo;

/// <summary>
/// The host-facing description a loaded <see cref="IHostedWorkflow"/> advertises about itself, so an execution
/// host can bind transports and route runs without knowing the workflow's generated input/output types.
/// </summary>
/// <param name="WorkflowId">The versioned workflow id (<c>{baseWorkflowId}-v{versionNumber}</c>) this executor runs.</param>
/// <param name="Sources">The API (OpenAPI) <c>sourceDescriptions</c> names the workflow's operation steps call, so the host can resolve an <c>IApiTransport</c> endpoint per source and fail fast when a binding is missing. AsyncAPI sources are listed in <see cref="MessageSources"/>, not here.</param>
/// <param name="MessageSources">The AsyncAPI (channel) <c>sourceDescriptions</c> the workflow's channel steps send/receive on — each with the transport protocol its source document declares (<c>servers[].protocol</c>, ADR 0051) — so the host can bind an <c>IMessageTransport</c> per channel source from that environment's channel credential.</param>
public readonly record struct WorkflowDescriptor(
    string WorkflowId,
    IReadOnlyList<string> Sources,
    IReadOnlyList<MessageSourceDescriptor> MessageSources)
{
    /// <summary>Gets a value indicating whether the workflow has an AsyncAPI channel step, so the host must supply
    /// a message transport per <see cref="MessageSources"/> entry to <see cref="IHostedWorkflow.RunAsync"/>.</summary>
    public bool NeedsMessageTransport => this.MessageSources.Count > 0;
}

/// <summary>
/// One AsyncAPI (channel) source a workflow binds: its <c>sourceDescriptions</c> name and the transport protocol
/// its source document declares (<c>servers[].protocol</c> — e.g. <c>nats</c>, <c>kafka</c>), baked at generation
/// time so the host's protocol-dispatched transport factory (ADR 0051) needs no document access at bind time.
/// </summary>
/// <param name="Name">The <c>sourceDescriptions</c> name of the channel source.</param>
/// <param name="Protocol">The transport protocol the source document's <c>servers</c> declare.</param>
public readonly record struct MessageSourceDescriptor(string Name, string Protocol);