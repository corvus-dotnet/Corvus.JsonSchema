// <copyright file="WorkflowDescriptor.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo;

/// <summary>
/// The host-facing description a loaded <see cref="IHostedWorkflow"/> advertises about itself, so an execution
/// host can bind transports and route runs without knowing the workflow's generated input/output types.
/// </summary>
/// <param name="WorkflowId">The versioned workflow id (<c>{baseWorkflowId}-v{versionNumber}</c>) this executor runs.</param>
/// <param name="NeedsMessageTransport">Whether the workflow has an AsyncAPI channel step, so the host must supply an <c>IMessageTransport</c> to <see cref="IHostedWorkflow.RunAsync"/>.</param>
/// <param name="Sources">The API (OpenAPI) <c>sourceDescriptions</c> names the workflow's operation steps call, so the host can resolve an <c>IApiTransport</c> endpoint per source and fail fast when a binding is missing. AsyncAPI sources are not listed here — they are served by the message transport (<see cref="NeedsMessageTransport"/>).</param>
public readonly record struct WorkflowDescriptor(
    string WorkflowId,
    bool NeedsMessageTransport,
    IReadOnlyList<string> Sources);