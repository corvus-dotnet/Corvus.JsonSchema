// <copyright file="WorkflowOperationBinder.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo11;
using Corvus.Text.Json.AsyncApi.CodeGeneration;

namespace Corvus.Text.Json.Arazzo.CodeGeneration;

/// <summary>
/// Binds a workflow step to the operation it targets — the one piece of information the emitter
/// cannot read from the typed Arazzo model itself (plan §3.1). The emitter reads a step's parameters,
/// criteria, request body, and outputs directly from the strongly-typed
/// <see cref="ArazzoDocument.StepObject"/>; it consults this binder only to turn an
/// <c>operationId</c>/<c>operationPath</c> into the generator's resolved operation (request/response
/// types and request-parameter metadata).
/// </summary>
public sealed class WorkflowOperationBinder
{
    private readonly IReadOnlyList<SourceDescriptionClient> clients;
    private readonly Dictionary<string, SourceDescriptionClient> byName;
    private readonly IReadOnlyList<SourceDescriptionChannels> channelSources;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkflowOperationBinder"/> class.
    /// </summary>
    /// <param name="clients">The generated client per OpenAPI source description.</param>
    /// <param name="channelSources">The generated channel operations per AsyncAPI source description (for channel steps).</param>
    public WorkflowOperationBinder(
        IReadOnlyList<SourceDescriptionClient> clients,
        IReadOnlyList<SourceDescriptionChannels>? channelSources = null)
    {
        ArgumentNullException.ThrowIfNull(clients);
        this.clients = clients;
        this.channelSources = channelSources ?? [];
        this.byName = new Dictionary<string, SourceDescriptionClient>(StringComparer.Ordinal);
        foreach (SourceDescriptionClient client in clients)
        {
            this.byName[client.Name] = client;
        }
    }

    /// <summary>
    /// Binds a step to its target.
    /// </summary>
    /// <param name="step">The step.</param>
    /// <returns>The binding: the resolved operation for an operation step, the sub-workflow id for a workflow step, or <see cref="StepTargetKind.None"/>.</returns>
    /// <exception cref="InvalidOperationException">An operation step references an operation no source description defines.</exception>
    public StepBinding Bind(in ArazzoDocument.StepObject step)
    {
        if (step.OperationId.IsNotUndefined())
        {
            return this.BindOperationId(step.OperationId.GetString()!);
        }

        if (step.OperationPath.IsNotUndefined())
        {
            return this.BindOperationPath(step.OperationPath.GetString()!);
        }

        if (step.WorkflowId.IsNotUndefined())
        {
            return new StepBinding(StepTargetKind.WorkflowId, null, step.WorkflowId.GetString());
        }

        if (step.ChannelPath.IsNotUndefined())
        {
            return this.BindChannel(step.ChannelPath.GetString()!, step);
        }

        return new StepBinding(StepTargetKind.None, null, null);
    }

    private StepBinding BindChannel(string channelPath, in ArazzoDocument.StepObject step)
    {
        // The step's action (send/receive) disambiguates two operations on the same channel address.
        OperationAction action = step.Action.IsNotUndefined() && step.Action.GetString() == "receive"
            ? OperationAction.Receive
            : OperationAction.Send;

        foreach (SourceDescriptionChannels source in this.channelSources)
        {
            foreach (AsyncApiChannelDescriptor channel in source.Channels)
            {
                if (channel.ChannelAddress == channelPath && channel.Action == action)
                {
                    return new StepBinding(StepTargetKind.ChannelPath, null, null, new ResolvedChannel(source.Name, channel));
                }
            }
        }

        throw new InvalidOperationException(
            $"No AsyncAPI source description defines a channel '{channelPath}' with action '{action.ToString().ToLowerInvariant()}'.");
    }

    private static string? ExtractSourceName(string operationPath)
    {
        // The expression form is {$sourceDescriptions.<name>.url}#/... — pull <name> from between
        // the marker and the following dot.
        const string marker = "$sourceDescriptions.";
        int start = operationPath.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0)
        {
            return null;
        }

        start += marker.Length;
        int end = operationPath.IndexOf('.', start);
        return end > start ? operationPath[start..end] : null;
    }

    private StepBinding BindOperationId(string operationId)
    {
        foreach (SourceDescriptionClient client in this.clients)
        {
            if (client.Resolver.TryResolveOperationId(operationId, out ResolvedOperation operation))
            {
                return new StepBinding(StepTargetKind.OperationId, operation, null);
            }
        }

        throw new InvalidOperationException($"No source description defines operationId '{operationId}'.");
    }

    private StepBinding BindOperationPath(string operationPath)
    {
        // Prefer the source named in the expression; the resolver only knows its own document.
        if (ExtractSourceName(operationPath) is { } sourceName
            && this.byName.TryGetValue(sourceName, out SourceDescriptionClient named))
        {
            if (named.Resolver.TryResolveOperationPath(operationPath, out ResolvedOperation operation))
            {
                return new StepBinding(StepTargetKind.OperationPath, operation, null);
            }

            throw new InvalidOperationException(
                $"operationPath '{operationPath}' does not resolve to an operation in source '{sourceName}'.");
        }

        // No (or unknown) source in the expression — try every client.
        foreach (SourceDescriptionClient client in this.clients)
        {
            if (client.Resolver.TryResolveOperationPath(operationPath, out ResolvedOperation operation))
            {
                return new StepBinding(StepTargetKind.OperationPath, operation, null);
            }
        }

        throw new InvalidOperationException(
            $"operationPath '{operationPath}' does not resolve to any source description operation.");
    }
}

/// <summary>
/// The result of binding a step to its target (plan §3.1).
/// </summary>
/// <param name="Kind">The kind of target the step invokes.</param>
/// <param name="Operation">The resolved operation, for an operation step; otherwise <see langword="null"/>.</param>
/// <param name="SubWorkflowId">The sub-workflow id, for a workflow step; otherwise <see langword="null"/>.</param>
public readonly record struct StepBinding(
    StepTargetKind Kind,
    ResolvedOperation? Operation,
    string? SubWorkflowId,
    ResolvedChannel? Channel = null);