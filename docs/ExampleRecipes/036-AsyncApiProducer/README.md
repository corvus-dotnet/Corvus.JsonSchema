# 036 — AsyncAPI Producer

Demonstrates how to **publish messages** using a generated AsyncAPI 3.0 producer. The code generator reads the [Streetlights](https://www.asyncapi.com/docs/tutorials/streetlights) spec and emits a strongly-typed `TurnOnProducer` that handles channel-address construction, payload validation, and authentication — you just build the payload and call `PublishTurnOnOffAsync`.

## What This Demonstrates

| Feature | Where |
|---------|-------|
| Typed payload construction via `Source` pattern | `TurnOnOffPayload.Source` with ref-struct builder |
| Channel parameter interpolation | `streetlightId` → zero-allocation UTF-8 address |
| Payload schema validation | `ValidationMode.Basic` or `.Detailed` |
| Authentication provider | `UserPasswordAuthenticationProvider` |
| In-memory transport for testing | `InMemoryMessageTransport` |

## Prerequisites

```bash
dotnet tool install --global Corvus.Json.Cli
```

## Generating the Code

```bash
corvusjson asyncapi-generate streetlights.json \
    --rootNamespace Streetlights.Client \
    --outputPath Generated
```

This produces:
- **`TurnOnProducer`** — typed producer for the `turnOn` operation with `PublishTurnOnOffAsync`
- **`ReceiveLightMeasurementConsumer`** — consumer for the `receiveLightMeasurement` operation (used in [037](../037-AsyncApiConsumer/))
- **`IReceiveLightMeasurementHandler`** — handler interface for incoming messages
- **Message wrappers** (`TurnOnTurnOnOffMessage`, `ReceiveLightMeasurementLightMeasuredMessage`)
- **16 model types** in `Models/` — `TurnOnOffPayload`, `LightMeasuredPayload`, `JsonDateTime`, etc.

## How the Generated Code Helps

### Payload Construction (Source Pattern)

The generated `TurnOnOffPayload.Source` uses a ref-struct builder that avoids allocating intermediate objects:

```csharp
await producer.PublishTurnOnOffAsync(
    payload: new TurnOnOffPayload.Source((ref TurnOnOffPayload.Builder b) =>
    {
        b.Create(command: "on"u8, sentAt: DateTimeOffset.UtcNow);
    }),
    streetlightId: "lamp-42");
```

### Channel Address Construction

The `streetlightId` parameter is part of the channel address template `smartylighting.streetlights.1.0.action.{streetlightId}.turn.on`. The generated code constructs the full address using zero-allocation UTF-8 byte manipulation with pooled buffers.

### Validation

Pass a `ValidationMode` to the producer constructor to control payload validation:

| Mode | Behaviour |
|------|-----------|
| `ValidationMode.None` | No validation (fastest, production hot path) |
| `ValidationMode.Basic` | Boolean pass/fail schema check |
| `ValidationMode.Detailed` | Full error diagnostics with JSON Pointer locations |

## Running

```bash
dotnet run -f net10.0
```

Expected output:

```text
Published turnOnOff to lamp-42
Channel: smartylighting.streetlights.1.0.action.lamp-42.turn.on
Payload: {"command":"on","sentAt":"2026-05-26T...+00:00"}
Published authenticated turnOnOff to lamp-99
Channel with param: smartylighting.streetlights.1.0.action.lamp-99.turn.on
```

## Related Recipes

- [037 — AsyncAPI Consumer](../037-AsyncApiConsumer/) — receiving and handling messages
- [038 — AsyncAPI End-to-End](../038-AsyncApiEndToEnd/) — full producer + consumer pipeline
- [039 — AsyncAPI Authentication](../039-AsyncApiAuthentication/) — all supported auth patterns
