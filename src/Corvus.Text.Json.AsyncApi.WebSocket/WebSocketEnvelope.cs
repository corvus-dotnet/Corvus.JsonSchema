// <copyright file="WebSocketEnvelope.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;

namespace Corvus.Text.Json.AsyncApi.WebSocket;

/// <summary>
/// The framing envelope used by the WebSocket message transport.
/// </summary>
[JsonSchemaTypeGenerator("Schemas/websocket-envelope.json")]
public readonly partial struct WebSocketEnvelope;