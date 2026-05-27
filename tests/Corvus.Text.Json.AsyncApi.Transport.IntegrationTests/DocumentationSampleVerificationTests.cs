using Confluent.Kafka;
using Corvus.Text.Json.AsyncApi.Amqp;
using Corvus.Text.Json.AsyncApi.Kafka;
using Corvus.Text.Json.AsyncApi.Mqtt;
using Corvus.Text.Json.AsyncApi.Nats;
using MQTTnet.Protocol;

namespace Corvus.Text.Json.AsyncApi.Transport.IntegrationTests;

/// <summary>
/// Verifies that documentation code samples compile correctly.
/// </summary>
[TestClass]
public class DocumentationSampleVerificationTests
{
    [TestMethod]
    public void KafkaTransportOptions_Sample1_Compiles()
    {
        KafkaTransportOptions options = new()
        {
            BootstrapServers = "localhost:9092",

            // Consumer group ID — all consumers with the same GroupId share offsets
            GroupId = "sensor-processor-v1",

            // What to do when no offset exists (new consumer group):
            //   Earliest - Start from the beginning
            //   Latest   - Start from newest messages
            AutoOffsetReset = AutoOffsetReset.Earliest,

            // Fine-grained control via ConsumerConfig:
            ConsumerConfig = new ConsumerConfig
            {
                // Automatically commit offsets after successful processing
                EnableAutoCommit = true,
                AutoCommitIntervalMs = 5000,

                // Or disable auto-commit for manual control:
                // EnableAutoCommit = false,
            },
        };

        // Verify options compile - don't actually connect
        Assert.IsNotNull(options);
    }

    [TestMethod]
    public void KafkaTransportOptions_Sample2_Compiles()
    {
        KafkaTransportOptions options = new()
        {
            GroupId = "order-processor",
            AutoOffsetReset = AutoOffsetReset.Earliest,

            ConsumerConfig = new ConsumerConfig
            {
                // Commit immediately after each message (safest, slowest)
                EnableAutoCommit = false,
            },
        };

        // After successful handler execution, offset is committed
        // If app crashes mid-processing, message will be redelivered
        // ReceiveOrderConsumer consumer = new(transport, handler);
        // await consumer.StartAsync();

        // Verify options compile - don't actually connect
        Assert.IsNotNull(options);
    }

    [TestMethod]
    public void AmqpTransportOptions_Sample1_Compiles()
    {
        AmqpTransportOptions options = new()
        {
            ConnectionUri = "amqp://guest:guest@localhost:5672/",

            // Durable queues survive broker restarts
            QueueDurable = true,
            ExchangeDurable = true,

            // Prefetch count — how many unacknowledged messages to buffer
            PrefetchCount = 10,

            // Dead-letter exchange for failed messages
            DeadLetterExchange = "sensor-errors",
        };

        // Verify options compile - don't actually connect
        Assert.IsNotNull(options);
    }

    [TestMethod]
    public void AmqpTransportOptions_Sample2_Compiles()
    {
        AmqpTransportOptions options = new()
        {
            QueueDurable = true,
            PrefetchCount = 1, // Process one message at a time
            DeadLetterExchange = "orders.dead-letter",
        };

        // If handler fails, message is sent to dead-letter exchange
        // If app crashes, unacknowledged message is redelivered
        // ReceiveOrderConsumer consumer = new(transport, handler);
        // await consumer.StartAsync();

        // Verify options compile - don't actually connect
        Assert.IsNotNull(options);
    }

    [TestMethod]
    public void MqttTransportOptions_Sample1_Compiles()
    {
        MqttTransportOptions options = new()
        {
            Host = "localhost",
            Port = 1883,

            // Persistent session — must use unique ClientId
            ClientId = "sensor-processor-001",
            CleanSession = false,

            // QoS level for all subscriptions
            QualityOfServiceLevel = MqttQualityOfServiceLevel.AtLeastOnce,
        };

        // Verify options compile - don't actually connect
        Assert.IsNotNull(options);
    }

    [TestMethod]
    public void MqttTransportOptions_Sample2_Compiles()
    {
        MqttTransportOptions options = new()
        {
            // CRITICAL: Must be stable across restarts
            ClientId = "order-processor-prod-01",
            CleanSession = false,
            QualityOfServiceLevel = MqttQualityOfServiceLevel.AtLeastOnce,
        };

        // Messages published while offline are delivered after restart
        // ReceiveOrderConsumer consumer = new(transport, handler);
        // await consumer.StartAsync();

        // Verify options compile - don't actually connect
        Assert.IsNotNull(options);
    }

    [TestMethod]
    public void MqttTransportOptions_Sample3_Compiles()
    {
        MqttTransportOptions options = new()
        {
            CleanSession = true, // Discard session on disconnect
            QualityOfServiceLevel = MqttQualityOfServiceLevel.AtMostOnce,
        };

        // Messages during downtime are lost — use for telemetry, logging
        // ReceiveTelemetryConsumer consumer = new(transport, handler);
        // await consumer.StartAsync();

        // Verify options compile - don't actually connect
        Assert.IsNotNull(options);
    }

    [TestMethod]
    public void NatsTransportOptions_Sample1_Compiles()
    {
        NatsTransportOptions options = new()
        {
            Url = "nats://localhost:4222",
            Name = "sensor-processor",
        };

        // Core NATS: messages are delivered to active subscribers only
        // No resumption support — offline = messages lost

        // Verify options compile - don't actually connect
        Assert.IsNotNull(options);
    }
}