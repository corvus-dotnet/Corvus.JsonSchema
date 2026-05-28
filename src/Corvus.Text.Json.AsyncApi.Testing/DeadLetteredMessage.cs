// <copyright file="DeadLetteredMessage.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.AsyncApi.Testing;

/// <summary>
/// Represents a message that was sent to a dead-letter channel via the <see cref="InMemoryMessageTransport"/>.
/// </summary>
/// <param name="DeadLetterChannel">The dead-letter channel it was sent to.</param>
/// <param name="OriginalChannel">The original channel the message arrived on.</param>
/// <param name="PayloadBytes">The serialized payload bytes.</param>
/// <param name="HeaderBytes">The serialized header bytes (empty if none).</param>
/// <param name="Exception">The exception that caused dead-lettering.</param>
public sealed record DeadLetteredMessage(
    string DeadLetterChannel,
    string OriginalChannel,
    byte[] PayloadBytes,
    byte[] HeaderBytes,
    Exception Exception);