# 038 — AsyncAPI End-to-End

Demonstrates the **full publish → transport → consume pipeline** using both the generated producer and consumer on a shared `InMemoryMessageTransport`. This is the integration test pattern for AsyncAPI-based messaging without a real broker.

## What This Demonstrates

| Feature | Where |
|---------|-------|
| Producer publishes typed messages | `TurnOnProducer.PublishTurnOnOffAsync` |
| Consumer receives via handler | `ReceiveLightMeasurementConsumer` + `IReceiveLightMeasurementHandler` |
| Shared transport captures and delivers | `InMemoryMessageTransport` |
| Schema validation rejects bad data | Invalid payload skipped before handler |
| Transport state inspection | `PublishedMessages`, `DeadLetteredMessages` |

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

## How It Works

The example sets up both sides of the messaging pipeline on the same in-memory transport:

1. **Consumer starts** — subscribes to the `lightMeasured` channel via the handler
2. **Producer publishes** — sends a `turnOnOff` command, captured by the transport
3. **Direct delivery** — simulates broker delivery of a sensor reading to the consumer
4. **Validation** — an invalid payload (`{"lumens":-5}`) is caught by schema validation and never reaches the handler
5. **Transport state** — inspect published and dead-lettered messages

This is the pattern you would use in integration tests: substitute `InMemoryMessageTransport` for your real transport (NATS, Kafka, AMQP) and verify your handler logic without broker infrastructure.

## Running

```bash
dotnet run -f net10.0
```

Expected output:

```text
Published to channel, consumer received: 0
  [Handler] lumens=1024, sentAt=2026-05-25T12:00:00Z
Delivered sensor reading, consumer received: 1
Last measurement: 1024 lumens
After bad data: handler still has 1 (invalid message skipped)

Transport state:
  Published messages: 1
  Dead-lettered: 1
  → smartylighting.streetlights.1.0.action.lamp-42.turn.on: {"command":"on",...}

Consumer stopped. End-to-end demo complete.
```

## Related Recipes

- [036 — AsyncAPI Producer](../036-AsyncApiProducer/) — producer details and Source pattern
- [037 — AsyncAPI Consumer](../037-AsyncApiConsumer/) — consumer details and error policy
- [039 — AsyncAPI Authentication](../039-AsyncApiAuthentication/) — all supported auth patterns
