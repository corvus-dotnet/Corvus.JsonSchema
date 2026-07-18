// <copyright file="SwitchMessages.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Generic;
using Corvus.Text.Json;
using Corvus.Text.Json.AsyncApi;

namespace Acme.Switches;

/// <summary>
/// A minimal generated-style AsyncAPI message payload type: <c>From</c> wraps a <see cref="JsonElement"/>
/// and <c>EvaluateSchema</c> validates it against this message's schema (an object with an <c>on</c>
/// property), mirroring the shape the send-side multi-message selection calls.
/// </summary>
/// <param name="value">The backing JSON value.</param>
public readonly struct TurnOn(JsonElement value)
{
    /// <summary>Wraps a JSON value as this message type.</summary>
    /// <param name="value">The JSON value.</param>
    /// <returns>The message.</returns>
    public static TurnOn From(in JsonElement value) => new(value);

    /// <summary>Validates the value against this message's schema.</summary>
    /// <returns><see langword="true"/> if the value is a <c>turnOn</c> message.</returns>
    public bool EvaluateSchema() => value.ValueKind == JsonValueKind.Object && value.TryGetProperty("on"u8, out _);
}

/// <summary>A second generated-style message payload type (an object with an <c>off</c> property).</summary>
/// <param name="value">The backing JSON value.</param>
public readonly struct TurnOff(JsonElement value)
{
    /// <summary>Wraps a JSON value as this message type.</summary>
    /// <param name="value">The JSON value.</param>
    /// <returns>The message.</returns>
    public static TurnOff From(in JsonElement value) => new(value);

    /// <summary>Validates the value against this message's schema.</summary>
    /// <returns><see langword="true"/> if the value is a <c>turnOff</c> message.</returns>
    public bool EvaluateSchema() => value.ValueKind == JsonValueKind.Object && value.TryGetProperty("off"u8, out _);
}

/// <summary>
/// A generated-style producer for a multi-message channel: one publish method per message. Records which
/// method the send-side selection actually called (by payload-schema validity) so a test can assert it.
/// </summary>
public sealed class SwitchProducer(IMessageTransport transport)
{
    private readonly IMessageTransport transport = transport;

    /// <summary>The messages published, in order, as <c>on</c>/<c>off</c> — reset per test.</summary>
    public static List<string> Published { get; } = [];

    /// <summary>Publishes a <c>turnOn</c> message.</summary>
    /// <param name="payload">The message payload.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A completed task.</returns>
    public ValueTask PublishTurnOnAsync(TurnOn payload, CancellationToken cancellationToken = default)
    {
        _ = this.transport;
        _ = payload;
        Published.Add("on");
        return ValueTask.CompletedTask;
    }

    /// <summary>Publishes a <c>turnOff</c> message.</summary>
    /// <param name="payload">The message payload.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A completed task.</returns>
    public ValueTask PublishTurnOffAsync(TurnOff payload, CancellationToken cancellationToken = default)
    {
        _ = this.transport;
        _ = payload;
        Published.Add("off");
        return ValueTask.CompletedTask;
    }
}
