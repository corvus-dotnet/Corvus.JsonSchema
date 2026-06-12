// <copyright file="WorkflowTransportRegistry.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo;
using Corvus.Text.Json.AsyncApi;
using Corvus.Text.Json.OpenApi;

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// The host's transport-binding configuration: maps each API source name a workflow declares to an
/// <see cref="IApiTransportFactory"/> (base URL + auth), plus the single shared <see cref="IMessageTransport"/>
/// that serves message steps and message triggers (design §8). It turns a <see cref="WorkflowDescriptor"/>
/// (the source names + message need a loaded workflow advertises) into the <see cref="WorkflowTransports"/>
/// the resumer runs with, and fails fast when a required binding is missing.
/// </summary>
/// <remarks>
/// The source names are host config, not baked into the Arazzo document, so the same workflow can bind to
/// different endpoints per environment. The map keys are the workflow's <c>sourceDescriptions[].name</c>
/// values; an entry's factory carries the base URL and auth (typically <c>HttpClientApiTransportFactory</c>).
/// A factory (not a shared transport) is held because the resumer disposes the API transport after each run.
/// </remarks>
public sealed class WorkflowTransportRegistry
{
    private readonly IReadOnlyDictionary<string, IApiTransportFactory> apiSources;
    private readonly IMessageTransport? messageTransport;

    /// <summary>Initializes a new instance of the <see cref="WorkflowTransportRegistry"/> class.</summary>
    /// <param name="apiSources">The configured API sources, keyed by the source name the workflow declares.</param>
    /// <param name="messageTransport">The shared message transport, or <see langword="null"/> if the host binds no message workflows.</param>
    public WorkflowTransportRegistry(
        IReadOnlyDictionary<string, IApiTransportFactory> apiSources,
        IMessageTransport? messageTransport = null)
    {
        ArgumentNullException.ThrowIfNull(apiSources);
        this.apiSources = apiSources;
        this.messageTransport = messageTransport;
    }

    /// <summary>Exposes this registry as a <see cref="WorkflowTransportBinder"/> for the resumer.</summary>
    /// <returns>A binder that resolves transports for a descriptor (and fails fast if a binding is missing).</returns>
    public WorkflowTransportBinder AsBinder() => this.Bind;

    /// <summary>
    /// Verifies the workflow can be bound — every required transport is configured — without constructing
    /// anything. A host calls this at load time so a misconfiguration surfaces before the first run.
    /// </summary>
    /// <param name="descriptor">The loaded workflow's descriptor.</param>
    /// <exception cref="WorkflowTransportBindingException">A required binding is missing or unsupported.</exception>
    public void Validate(WorkflowDescriptor descriptor)
    {
        _ = this.ResolveApiSource(descriptor);
        this.EnsureMessageTransport(descriptor);
    }

    /// <summary>Resolves the transports a run of <paramref name="descriptor"/> needs.</summary>
    /// <param name="descriptor">The loaded workflow's descriptor.</param>
    /// <returns>The API transport (fresh, caller-disposed) and the shared message transport if needed.</returns>
    /// <exception cref="WorkflowTransportBindingException">A required binding is missing or unsupported.</exception>
    public WorkflowTransports Bind(WorkflowDescriptor descriptor)
    {
        string apiSource = this.ResolveApiSource(descriptor);
        this.EnsureMessageTransport(descriptor);

        IApiTransport api = this.apiSources[apiSource].CreateTransport();
        IMessageTransport? message = descriptor.NeedsMessageTransport ? this.messageTransport : null;
        return new WorkflowTransports(api, message);
    }

    // The single declared source name that has a configured API transport. The runtime drives every step
    // through one IApiTransport, so exactly one of the workflow's sources must bind (sources without an entry
    // — e.g. an AsyncAPI message source — are served by the message transport, not here).
    private string ResolveApiSource(WorkflowDescriptor descriptor)
    {
        string? found = null;
        foreach (string source in descriptor.Sources)
        {
            if (this.apiSources.ContainsKey(source))
            {
                if (found is not null)
                {
                    throw new WorkflowTransportBindingException(
                        $"Workflow '{descriptor.WorkflowId}' binds more than one API source ('{found}', '{source}'); multi-source binding is not yet supported.");
                }

                found = source;
            }
        }

        return found ?? throw new WorkflowTransportBindingException(
            $"Workflow '{descriptor.WorkflowId}' requires an API transport, but none of its sources [{string.Join(", ", descriptor.Sources)}] has a configured binding.");
    }

    private void EnsureMessageTransport(WorkflowDescriptor descriptor)
    {
        if (descriptor.NeedsMessageTransport && this.messageTransport is null)
        {
            throw new WorkflowTransportBindingException(
                $"Workflow '{descriptor.WorkflowId}' requires a message transport, but none is configured.");
        }
    }
}