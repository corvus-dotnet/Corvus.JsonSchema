## Summary

This PR adds optional runtime delivery context to AsyncAPI-generated consumers and propagates that context through the available Corvus transport adapters.

The default generated API remains unchanged. The new behavior is enabled explicitly with:

```text
--with-message-context
```

## Generated API

Without the option, generated handlers retain the existing signature:

```csharp
ValueTask HandleMessageAsync(
    Payload payload,
    JsonElement headers,
    CancellationToken cancellationToken);
```

With the option enabled, generated handlers receive a `MessageDeliveryContext`:

```csharp
ValueTask HandleMessageAsync(
    Payload payload,
    MessageDeliveryContext context,
    CancellationToken cancellationToken);
```

The generated consumer continues to handle payload deserialization and validation. The transport provides the runtime context for each delivered message.

## MessageDeliveryContext

The context is transport-independent at its core:

```csharp
public readonly struct MessageDeliveryContext
{
    public ReadOnlyMemory<byte> ChannelUtf8 { get; init; }

    public JsonElement Headers { get; init; }

    public object? NativeMessage { get; init; }
}
```

`ChannelUtf8` contains the channel or subscription address used by the consumer.

`Headers` contains message headers exposed through a protocol-neutral `JsonElement`.

`NativeMessage` is an optional adapter-specific escape hatch. It allows advanced consumers to access transport-specific information without making the normal generated API transport-specific.

This is particularly useful when subscribing to a templated channel using a wildcard value such as `*` with NATS. The current generated handler receives the payload but has no way to determine the actual subject that matched the wildcard. The native message exposed through the delivery context makes that concrete subject available.

For example, a NATS handler can access the native message when it needs protocol-specific metadata:

```csharp
public ValueTask HandleMessageAsync(
    Payload payload,
    MessageDeliveryContext context,
    CancellationToken cancellationToken)
{
    NatsMsg<byte[]> message = (NatsMsg<byte[]>)context.NativeMessage!;
    string subject = message.Subject;

    return ValueTask.CompletedTask;
}
```

## Adapter support

Delivery context propagation is implemented for:

- NATS Core.
- NATS JetStream.
- AMQP/RabbitMQ.
- Azure Service Bus.
- Kafka.
- MQTT.
- WebSocket.
- Instrumented transports.

The native message exposed by `NativeMessage` is adapter-specific. It may be a NATS message, RabbitMQ delivery event, Kafka consume result, Azure Service Bus message, MQTT receive event, or WebSocket envelope bytes.

## Backwards compatibility

The existing `IMessageTransport` methods remain available.

The context-aware overloads use default interface implementations to adapt existing transport implementations. Existing custom transport implementations therefore do not need to implement the new overloads immediately.

Existing generated consumers continue to work unchanged unless the opt-in generator option is enabled.

## Tests

Added Testcontainers-backed integration coverage for:

- NATS.
- RabbitMQ.
- Kafka.
- MQTT/Mosquitto.
- Azure Service Bus.

The NATS integration test verifies that runtime delivery metadata is available to the handler.

All transport projects and the transport integration-test project build successfully. The NATS Testcontainers delivery-context test also passes.

## API discussion

I am not certain that this is the final ideal API, particularly the use of `object? NativeMessage` as the transport-specific escape hatch.

I chose this design because it keeps the common generated API transport-independent while still allowing applications to access native protocol objects when they need information that cannot be represented generically.

I am open to changing the API based on feedback, including the shape of the delivery context or the way native transport messages are exposed.
