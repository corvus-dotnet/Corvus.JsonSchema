// <copyright file="ResolvedChannel.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.AsyncApi.CodeGeneration;

namespace Corvus.Text.Json.Arazzo.CodeGeneration;

/// <summary>
/// An AsyncAPI channel operation a workflow step targets, resolved against an AsyncAPI source
/// description: the generator's <see cref="AsyncApiChannelDescriptor"/> (channel address, action,
/// generated producer class + publish method, message payload types) paired with the source it came
/// from. The Arazzo generator adds no naming of its own; it carries the description through verbatim.
/// </summary>
/// <param name="SourceName">The <c>name</c> of the source description that owns the channel.</param>
/// <param name="Channel">The generator's description of the channel operation.</param>
public readonly record struct ResolvedChannel(
    string SourceName,
    AsyncApiChannelDescriptor Channel);

/// <summary>
/// The generated AsyncAPI channel operations for a single source description (the AsyncAPI analogue of
/// <see cref="SourceDescriptionClient"/>), used by the <see cref="WorkflowOperationBinder"/> to resolve
/// a step's <c>channelPath</c>.
/// </summary>
/// <param name="Name">The source description name (must match the Arazzo <c>sourceDescriptions</c> name).</param>
/// <param name="Channels">The channel operations the source exposes.</param>
public readonly record struct SourceDescriptionChannels(
    string Name,
    IReadOnlyList<AsyncApiChannelDescriptor> Channels);