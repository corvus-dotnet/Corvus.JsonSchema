// <copyright file="WorkflowTransportRegistry.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo;
using Corvus.Text.Json.AsyncApi;
using Corvus.Text.Json.OpenApi;

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// The host's transport-binding configuration: maps each API source name a workflow declares to an
/// <see cref="IApiTransportFactory"/> (base URL + auth), plus an <see cref="IMessageTransport"/> per channel
/// source that serves message steps and message triggers (design §8, ADR 0051). It turns a
/// <see cref="WorkflowDescriptor"/> (the source names a loaded workflow advertises) into the
/// <see cref="WorkflowTransports"/> the resumer runs with, and fails fast when a required binding is missing.
/// </summary>
/// <remarks>
/// The source names are host config, not baked into the Arazzo document, so the same workflow can bind to
/// different endpoints per environment. The map keys are the workflow's <c>sourceDescriptions[].name</c>
/// values; an API entry's factory carries the base URL and auth (typically <c>HttpClientApiTransportFactory</c>).
/// A factory (not a shared transport) is held for API sources because the resumer disposes the API transport
/// after each run; message transports are shared, long-lived connections owned by the host.
/// </remarks>
public sealed class WorkflowTransportRegistry
{
    private readonly IReadOnlyDictionary<string, IApiTransportFactory> apiSources;
    private readonly IReadOnlyDictionary<string, IMessageTransport> messageSources;

    /// <summary>Initializes a new instance of the <see cref="WorkflowTransportRegistry"/> class.</summary>
    /// <param name="apiSources">The configured API sources, keyed by the source name the workflow declares.</param>
    /// <param name="messageSources">The configured message transports, keyed by the channel-source name the
    /// workflow declares, or <see langword="null"/> if the host binds no message workflows.</param>
    public WorkflowTransportRegistry(
        IReadOnlyDictionary<string, IApiTransportFactory> apiSources,
        IReadOnlyDictionary<string, IMessageTransport>? messageSources = null)
    {
        ArgumentNullException.ThrowIfNull(apiSources);
        this.apiSources = apiSources;
        this.messageSources = messageSources ?? WorkflowTransports.NoMessageTransports;
    }

    /// <summary>Exposes this registry as a <see cref="WorkflowTransportBinder"/> for the resumer.</summary>
    /// <returns>A binder that resolves transports for a descriptor (and fails fast if a binding is missing).</returns>
    public WorkflowTransportBinder AsBinder() => this.Bind;

    /// <summary>
    /// Verifies the workflow can be bound — every API and channel source it declares has a configured transport —
    /// without constructing anything. A host calls this at load time so a misconfiguration surfaces before the
    /// first run.
    /// </summary>
    /// <param name="descriptor">The loaded workflow's descriptor.</param>
    /// <exception cref="WorkflowTransportBindingException">A required binding is missing.</exception>
    public void Validate(WorkflowDescriptor descriptor)
    {
        foreach (string source in descriptor.Sources)
        {
            this.RequireApiSource(descriptor, source);
        }

        foreach (MessageSourceDescriptor source in descriptor.MessageSources)
        {
            this.RequireMessageSource(descriptor, source.Name);
        }
    }

    /// <summary>Resolves the transports a run of <paramref name="descriptor"/> needs.</summary>
    /// <param name="descriptor">The loaded workflow's descriptor.</param>
    /// <param name="runTags">The run's security tags — unused here (this registry's factories carry static auth); a
    /// credential-aware binder consumes them. Accepted so the registry satisfies <see cref="WorkflowTransportBinder"/>.</param>
    /// <returns>One fresh, caller-disposed API transport per declared source, plus the shared message transport per channel source.</returns>
    /// <exception cref="WorkflowTransportBindingException">A required binding is missing.</exception>
    public WorkflowTransports Bind(WorkflowDescriptor descriptor, SecurityTagSet runTags = default)
    {
        _ = runTags;

        var apiTransports = new Dictionary<string, IApiTransport>(descriptor.Sources.Count, StringComparer.Ordinal);
        foreach (string source in descriptor.Sources)
        {
            apiTransports[source] = this.RequireApiSource(descriptor, source).CreateTransport();
        }

        if (descriptor.MessageSources.Count == 0)
        {
            return new WorkflowTransports(apiTransports, WorkflowTransports.NoMessageTransports);
        }

        var messageTransports = new Dictionary<string, IMessageTransport>(descriptor.MessageSources.Count, StringComparer.Ordinal);
        foreach (MessageSourceDescriptor source in descriptor.MessageSources)
        {
            messageTransports[source.Name] = this.RequireMessageSource(descriptor, source.Name);
        }

        return new WorkflowTransports(apiTransports, messageTransports);
    }

    private IApiTransportFactory RequireApiSource(WorkflowDescriptor descriptor, string source)
        => this.apiSources.TryGetValue(source, out IApiTransportFactory? factory)
            ? factory
            : throw new WorkflowTransportBindingException(
                $"Workflow '{descriptor.WorkflowId}' requires API source '{source}', which has no configured transport binding.");

    private IMessageTransport RequireMessageSource(WorkflowDescriptor descriptor, string source)
        => this.messageSources.TryGetValue(source, out IMessageTransport? transport)
            ? transport
            : throw new WorkflowTransportBindingException(
                $"Workflow '{descriptor.WorkflowId}' requires channel source '{source}', which has no configured message transport.");
}