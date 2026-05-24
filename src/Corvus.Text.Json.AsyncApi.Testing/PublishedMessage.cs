// <copyright file="PublishedMessage.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.AsyncApi.Testing;

/// <summary>
/// Represents a message that was published via the <see cref="InMemoryMessageTransport"/>.
/// </summary>
/// <param name="Channel">The channel the message was published to.</param>
/// <param name="PayloadBytes">The serialized payload bytes.</param>
/// <param name="HeaderBytes">The serialized header bytes (empty if none).</param>
public sealed record PublishedMessage(string Channel, byte[] PayloadBytes, byte[] HeaderBytes);