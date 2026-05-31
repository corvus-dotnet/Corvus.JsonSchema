// <copyright file="Program.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json.AsyncApi;
using Corvus.Text.Json.AsyncApi.Testing;
using Streetlights.Client;
using Streetlights.Client.Models;

// ── Setting up the transport ─────────────────────────────────────────────────
// In production, you would use a real transport (NATS, Kafka, AMQP, etc.).
// Here we use InMemoryMessageTransport for demonstration — it captures
// published messages so we can inspect them.
await using InMemoryMessageTransport transport = new();

// ── Creating the producer ────────────────────────────────────────────────────
// The generated TurnOnProducer wraps the transport and provides a typed publish
// method. The second parameter controls schema validation:
//   • ValidationMode.None — no validation (fastest, production hot path)
//   • ValidationMode.Basic — boolean pass/fail schema check
//   • ValidationMode.Detailed — full error diagnostics with JSON Pointer locations
TurnOnProducer producer = new(transport, ValidationMode.Basic);

// ── Publishing a message ─────────────────────────────────────────────────────
// The publish method accepts a strongly-typed payload via the Source pattern.
// The Source delegate builds the payload using a ref-struct builder — this
// avoids allocating intermediate objects.
await producer.PublishTurnOnOffAsync(
    payload: new TurnOnOffPayload.Source((ref TurnOnOffPayload.Builder b) =>
    {
        b.Create(command: "on"u8, sentAt: DateTimeOffset.UtcNow);
    }),
    streetlightId: "lamp-42");

Console.WriteLine("Published turnOnOff to lamp-42");

// ── Inspecting what was published ────────────────────────────────────────────
// The InMemoryMessageTransport captures all published messages for verification.
PublishedMessage msg = transport.PublishedMessages[0];
Console.WriteLine($"Channel: {msg.Channel}");
Console.WriteLine($"Payload: {Encoding.UTF8.GetString(msg.PayloadBytes)}");

// ── Publishing with authentication ──────────────────────────────────────────
// When your spec defines security schemes, pass an auth provider to the producer.
// The generated code calls AuthenticateAsync before each publish.
IMessageAuthenticationProvider auth = new UserPasswordAuthenticationProvider(
    username: "service-account",
    password: "kafka-secret");

TurnOnProducer authenticatedProducer = new(transport, ValidationMode.Basic, authProvider: auth);

await authenticatedProducer.PublishTurnOnOffAsync(
    payload: new TurnOnOffPayload.Source((ref TurnOnOffPayload.Builder b) =>
    {
        b.Create(command: "off"u8, sentAt: DateTimeOffset.UtcNow);
    }),
    streetlightId: "lamp-99");

Console.WriteLine("Published authenticated turnOnOff to lamp-99");

// ── Channel parameters ───────────────────────────────────────────────────────
// The streetlightId parameter is part of the channel address template:
//   smartylighting.streetlights.1.0.action.{streetlightId}.turn.on
// The generated code constructs the full channel address using zero-allocation
// UTF-8 byte manipulation with pooled buffers — no string concatenation.
PublishedMessage msg2 = transport.PublishedMessages[1];
Console.WriteLine($"Channel with param: {msg2.Channel}");
// Output: smartylighting.streetlights.1.0.action.lamp-99.turn.on